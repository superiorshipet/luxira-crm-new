using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.Orders.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Luxira.Api.Features.DeliveryCompanies.Services;

public enum CourierConfirmOutcome { Sent, Blocked, Deferred }
public sealed record CourierConfirmResult(CourierConfirmOutcome Outcome, string Message, string? ExternalReference = null);
public sealed record SandoogOrderDetails(bool Success, string? Status = null, string? Reason = null, string? ReasonCode = null, string? DeliveryLabelUrl = null, string? FulfillmentId = null, string? Error = null);

public sealed class CourierDispatchService(ApplicationDbContext context, IConfiguration configuration, IHttpClientFactory clients, IMemoryCache cache, ILogger<CourierDispatchService> logger)
{
    private static readonly JsonSerializerOptions CourierPayloadOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly SemaphoreSlim SandoogAuthLock = new(1, 1);
    private static readonly SemaphoreSlim CamexAuthLock = new(1, 1);
    private static readonly Dictionary<string, string> SandoogCities = new(StringComparer.Ordinal)
    {
        ["بغداد"]="baghdad", ["كربلاء"]="karbala", ["كوت"]="koot", ["دهوك"]="dihok", ["صلاح الدين"]="saladin", ["ميسان"]="maysan", ["كركوك"]="karkok", ["بابل"]="babel", ["ذي قار"]="zeeqar",
        ["الموصل"]="mosul", ["البصرة"]="basra", ["النجف"]="najaf", ["السليمانية"]="solaimania", ["الكوت"]="koot", ["الأنبار"]="anbar", ["أربيل"]="arbeel", ["سماوه"]="simawa", ["ديالي"]="diyala", ["الديوانية"]="diwania"
    };

    public int DeliveryCompanyId(string courier) => configuration.GetValue<int?>($"{Section(courier)}:DeliveryCompanyId") ?? 0;
    public bool IsConfigured(string courier) => DeliveryCompanyId(courier) > 0 && (configuration.GetValue<bool?>($"{Section(courier)}:Enabled") ?? false) && courier switch
    {
        "sandoog" => !string.IsNullOrWhiteSpace(configuration["Sandoog:BaseUrl"]) && !string.IsNullOrWhiteSpace(configuration["Sandoog:ApiKey"]) && !string.IsNullOrWhiteSpace(configuration["Sandoog:EntityId"]),
        "camex" => !string.IsNullOrWhiteSpace(configuration["Camex:BaseUrl"]) && !string.IsNullOrWhiteSpace(configuration["Camex:ProviderKey"]) && !string.IsNullOrWhiteSpace(configuration["Camex:ClientKey"]),
        _ => false
    };

    public IQueryable<Order> PendingOrders(string courier)
    {
        var companyId = DeliveryCompanyId(courier);
        return courier switch
        {
            "sandoog" => context.Orders.Where(order => order.DeliveryCompanyId == companyId && order.SandoogConfirmedAt == null && order.SandoogOrderId == null && !order.SandoogLegacyManual && !order.IsHidden && order.OrderStatus == OrderStatusCodes.New),
            "camex" => context.Orders.Where(order => order.DeliveryCompanyId == companyId && order.CamexConfirmedAt == null && order.CamexTrackingNumber == null && !order.CamexLegacyManual && !order.IsHidden && order.OrderStatus == OrderStatusCodes.New),
            _ => context.Orders.Where(_ => false)
        };
    }

    public async Task<string?> DescribeDataProblem(string courier, Order order, CancellationToken ct)
    {
        if (courier == "sandoog")
        {
            if (!SandoogCities.ContainsKey(order.State?.Trim() ?? string.Empty)) return "غير مرتبطة بمحافظة لدى صندوق";
            if (NormalizeIraqiPhone(order.TelephoneNumber) is null) return "رقم الهاتف غير صالح";
            return null;
        }
        if (courier != "camex") return "شركة التوصيل غير معروفة";
        var city = await ResolveCamexCity(order.State, ct); if (city is null) return "غير مرتبطة بمدينة لدى كامكس";
        var store = await context.CamexStoreMappings.AsNoTracking().FirstOrDefaultAsync(item => item.ManufacturingCompanyId == order.ManufacturingCompanyId && item.CamexStoreName != null, ct);
        if (store is null) return "المتجر غير مرتبط بمتجر لدى كامكس";
        if (NormalizeLibyanPhone(order.TelephoneNumber) is null) return "رقم الهاتف غير صالح";
        if (string.IsNullOrWhiteSpace(order.Address)) return "العنوان فارغ";
        if (order.Address.Trim().Length > 50) return $"العنوان أطول من 50 حرفًا ({order.Address.Trim().Length})";
        var hasItems = await context.OrderWarehouses.AsNoTracking().AnyAsync(item => item.OrderId == order.Id && item.Amount > 0, ct);
        return hasItems ? null : "الطلب لا يحتوي على أي منتجات";
    }

    public async Task<CourierConfirmResult> ConfirmAsync(string courier, int orderId, CancellationToken ct)
    {
        courier = courier.Trim().ToLowerInvariant();
        if (courier is not ("sandoog" or "camex")) return new(CourierConfirmOutcome.Blocked, "شركة التوصيل غير معروفة.");
        if (!IsConfigured(courier)) return new(CourierConfirmOutcome.Blocked, $"إعدادات {Section(courier)} غير مكتملة.");
        var companyId = DeliveryCompanyId(courier);
        if (!await context.DeliveryCompanies.AsNoTracking().AnyAsync(company => company.Id == companyId && company.IsApiIntegrationEnabled, ct)) return new(CourierConfirmOutcome.Blocked, "الربط الآلي مع شركة التوصيل متوقف حاليًا.");
        var order = await context.Orders.Include(item => item.OrderWarehouses).ThenInclude(item => item.Warehouse).ThenInclude(item => item!.SubWarehouse).FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null || !IsEligible(courier, order, companyId)) return new(CourierConfirmOutcome.Blocked, "هذا الطلب غير مؤهل للإرسال.");
        var problem = await DescribeDataProblem(courier, order, ct); if (problem is not null) return new(CourierConfirmOutcome.Blocked, problem);
        var now = DateTime.UtcNow;
        var claimed = courier == "sandoog"
            ? await context.Orders.Where(item => item.Id == orderId && item.SandoogConfirmedAt == null && item.SandoogOrderId == null && !item.SandoogLegacyManual && !item.IsHidden && item.DeliveryCompanyId == companyId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogConfirmedAt, now).SetProperty(item => item.SandoogLastAttemptAt, now).SetProperty(item => item.SandoogSendAttempts, item => item.SandoogSendAttempts + 1), ct)
            : await context.Orders.Where(item => item.Id == orderId && item.CamexConfirmedAt == null && item.CamexTrackingNumber == null && !item.CamexLegacyManual && !item.IsHidden && item.DeliveryCompanyId == companyId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.CamexConfirmedAt, now).SetProperty(item => item.CamexLastAttemptAt, now).SetProperty(item => item.CamexSendAttempts, item => item.CamexSendAttempts + 1), ct);
        if (claimed == 0) return new(CourierConfirmOutcome.Blocked, "تم تأكيد الطلب من مستخدم آخر بالفعل.");
        try
        {
            var external = courier == "sandoog" ? await SendSandoog(order, ct) : await SendCamex(order, ct);
            if (string.IsNullOrWhiteSpace(external)) return new(CourierConfirmOutcome.Deferred, "تم تأكيد الطلب، لكن تعذر إرساله الآن. ستتم إعادة المحاولة تلقائيًا.");
            if (courier == "sandoog") await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogOrderId, external), ct);
            else if (long.TryParse(external, out var tracking)) await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.CamexTrackingNumber, tracking), ct); else throw new InvalidOperationException("CAMEX returned an invalid tracking number.");
            return new(CourierConfirmOutcome.Sent, "تم تأكيد الطلب وإرساله إلى شركة التوصيل بنجاح.", external);
        }
        catch (CourierDataException exception)
        {
            if (courier == "sandoog") await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogConfirmedAt, (DateTime?)null).SetProperty(item => item.SandoogSendAttempts, item => Math.Max(0, item.SandoogSendAttempts - 1)), ct);
            else await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.CamexConfirmedAt, (DateTime?)null).SetProperty(item => item.CamexSendAttempts, item => Math.Max(0, item.CamexSendAttempts - 1)), ct);
            return new(CourierConfirmOutcome.Blocked, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Courier send failed for {Courier} order {OrderId}; claim retained for retry", courier, orderId);
            return new(CourierConfirmOutcome.Deferred, "تم تأكيد الطلب، لكن تعذر إرساله الآن. ستتم إعادة المحاولة تلقائيًا.");
        }
    }

    public IQueryable<Order> RetryCandidates(string courier, DateTime cutoffUtc, int maximumAttempts)
    {
        var companyId = DeliveryCompanyId(courier);
        return courier == "sandoog"
            ? context.Orders.Where(item => item.DeliveryCompanyId == companyId && item.SandoogConfirmedAt != null && item.SandoogOrderId == null && !item.SandoogLegacyManual && !item.IsHidden && item.SandoogSendAttempts < maximumAttempts && (item.SandoogLastAttemptAt == null || item.SandoogLastAttemptAt < cutoffUtc))
            : context.Orders.Where(item => item.DeliveryCompanyId == companyId && item.CamexConfirmedAt != null && item.CamexTrackingNumber == null && !item.CamexLegacyManual && !item.IsHidden && item.CamexSendAttempts < maximumAttempts && (item.CamexLastAttemptAt == null || item.CamexLastAttemptAt < cutoffUtc));
    }

    public async Task<CourierConfirmResult> RetryAsync(string courier, int orderId, CancellationToken ct)
    {
        courier = courier.Trim().ToLowerInvariant();
        if (courier is not ("sandoog" or "camex") || !IsConfigured(courier)) return new(CourierConfirmOutcome.Blocked, "إعدادات شركة التوصيل غير مكتملة.");
        var order = await context.Orders.Include(item => item.OrderWarehouses).ThenInclude(item => item.Warehouse).ThenInclude(item => item!.SubWarehouse).FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return new(CourierConfirmOutcome.Blocked, "الطلب غير موجود.");
        var generation = courier == "sandoog" ? order.SandoogSendGeneration : order.CamexSendGeneration;
        var now = DateTime.UtcNow;
        var claimed = courier == "sandoog"
            ? await context.Orders.Where(item => item.Id == orderId && item.SandoogConfirmedAt != null && item.SandoogOrderId == null && item.SandoogSendGeneration == generation)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogSendGeneration, generation + 1).SetProperty(item => item.SandoogLastAttemptAt, now).SetProperty(item => item.SandoogSendAttempts, item => item.SandoogSendAttempts + 1), ct)
            : await context.Orders.Where(item => item.Id == orderId && item.CamexConfirmedAt != null && item.CamexTrackingNumber == null && item.CamexSendGeneration == generation)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.CamexSendGeneration, generation + 1).SetProperty(item => item.CamexLastAttemptAt, now).SetProperty(item => item.CamexSendAttempts, item => item.CamexSendAttempts + 1), ct);
        if (claimed == 0) return new(CourierConfirmOutcome.Blocked, "توجد محاولة أخرى قيد التنفيذ.");
        try
        {
            var external = courier == "sandoog" ? await SendSandoog(order, ct) : await SendCamex(order, ct);
            if (string.IsNullOrWhiteSpace(external)) return new(CourierConfirmOutcome.Deferred, "تعذر الإرسال وستتم إعادة المحاولة.");
            if (courier == "sandoog") await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogOrderId, external), ct);
            else if (long.TryParse(external, out var tracking)) await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.CamexTrackingNumber, tracking), ct);
            else throw new InvalidOperationException("CAMEX returned an invalid tracking number.");
            return new(CourierConfirmOutcome.Sent, "تمت إعادة الإرسال بنجاح.", external);
        }
        catch (CourierDataException exception)
        {
            if (courier == "sandoog") await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogConfirmedAt, (DateTime?)null), ct);
            else await context.Orders.Where(item => item.Id == orderId).ExecuteUpdateAsync(update => update.SetProperty(item => item.CamexConfirmedAt, (DateTime?)null), ct);
            return new(CourierConfirmOutcome.Blocked, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Courier retry failed for {Courier} order {OrderId}", courier, orderId);
            return new(CourierConfirmOutcome.Deferred, "تعذر الإرسال وستتم إعادة المحاولة.");
        }
    }

    private async Task<string?> SendSandoog(Order order, CancellationToken ct)
    {
        if (!(configuration.GetValue<bool?>("Sandoog:Enabled") ?? false)) return null;
        var state = order.State?.Trim() ?? string.Empty; if (!SandoogCities.TryGetValue(state, out var city)) throw new CourierDataException("مدينة الطلب غير مرتبطة بمحافظة لدى صندوق.");
        var phone = NormalizeIraqiPhone(order.TelephoneNumber) ?? throw new CourierDataException("رقم الهاتف غير صالح لصندوق.");
        var items = order.OrderWarehouses.Where(line => line.Amount > 0 && !string.IsNullOrWhiteSpace(line.Warehouse?.SubWarehouse?.ProductCode)).Select(line => new { sku = line.Warehouse!.SubWarehouse!.ProductCode, quantity = line.Amount }).ToList();
        if (items.Count == 0) throw new CourierDataException("لا توجد أصناف مسجلة برمز منتج صالح لصندوق.");
        var entity = order.ManufacturingCompanyId.HasValue ? configuration[$"Sandoog:EntityIdByStore:{order.ManufacturingCompanyId}"] : null; entity = string.IsNullOrWhiteSpace(entity) ? configuration["Sandoog:EntityId"] : entity;
        var payload = new { customer = new { name = order.CustomerName, phone, state = city, address = order.Address, second_phone = NormalizeIraqiPhone(order.SecondTelephoneNumber) }, delivery = new { delivery_type = configuration["Sandoog:DeliveryType"] ?? "Standard", delivery_region = "Center", delivery_items = items }, payment = new { total_price = order.TotalPrice, payment_charge_type = configuration["Sandoog:PaymentChargeType"] ?? "Customer", amount_include_delivery_charge = true }, entity_id = entity, external_reference = order.Id.ToString(CultureInfo.InvariantCulture), notes = BuildNotes(order.Notes) };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await SandoogToken(ct); if (token is null) return null;
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl("Sandoog") + "/orders") { Content = JsonContent.Create(payload, options: CourierPayloadOptions) }; request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { cache.Remove("courier:sandoog:token"); continue; }
            if (body.Contains("Duplicated request", StringComparison.OrdinalIgnoreCase)) { logger.LogWarning("Sandoog already has order {OrderId}; waiting for webhook to recover provider id", order.Id); return null; }
            if (!response.IsSuccessStatusCode) { if (body.Contains("stock", StringComparison.OrdinalIgnoreCase) || body.Contains("quantity", StringComparison.OrdinalIgnoreCase)) throw new CourierDataException(body); throw new HttpRequestException($"Sandoog returned {(int)response.StatusCode}."); }
            using var json = JsonDocument.Parse(body); return json.RootElement.ValueKind == JsonValueKind.String ? json.RootElement.GetString() : json.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        return null;
    }

    private async Task<string?> SendCamex(Order order, CancellationToken ct)
    {
        if (!(configuration.GetValue<bool?>("Camex:Enabled") ?? false)) return null;
        var city = await ResolveCamexCity(order.State, ct) ?? throw new CourierDataException("مدينة الطلب غير مرتبطة بكامكس.");
        var store = await context.CamexStoreMappings.AsNoTracking().FirstOrDefaultAsync(item => item.ManufacturingCompanyId == order.ManufacturingCompanyId && item.CamexStoreName != null, ct) ?? throw new CourierDataException("المتجر غير مرتبط بكامكس.");
        if (string.IsNullOrWhiteSpace(store.CamexStoreName)) throw new CourierDataException("المتجر غير مرتبط بكامكس.");
        var phone = NormalizeLibyanPhone(order.TelephoneNumber) ?? throw new CourierDataException("رقم الهاتف غير صالح لكامكس.");
        var address = order.Address?.Trim();
        if (string.IsNullOrWhiteSpace(address)) throw new CourierDataException("عنوان الطلب فارغ.");
        if (address.Length > 50) throw new CourierDataException($"العنوان أطول من 50 حرفًا ({address.Length}).");
        var lines = await context.OrderWarehouses.AsNoTracking().Where(item => item.OrderId == order.Id).Join(context.Warehouses.AsNoTracking(), line => line.WarehouseId, warehouse => warehouse.Id, (line, warehouse) => new { line.Amount, warehouse.Name }).ToListAsync(ct);
        var itemCount = lines.Sum(line => line.Amount);
        if (itemCount <= 0) throw new CourierDataException("الطلب لا يحتوي على منتجات.");
        var description = string.Join(" + ", lines.Where(line => !string.IsNullOrWhiteSpace(line.Name)).Select(line => line.Amount == 1 ? line.Name!.Trim() : $"{line.Name!.Trim()} x{line.Amount}"));
        if (string.IsNullOrWhiteSpace(description)) throw new CourierDataException("لا يوجد وصف صالح لمنتجات الطلب.");
        if (description.Length > 80) description = description[..80];
        var payload = new { cityId = city.CamexCityId, areaName = city.AreaName, noItems = itemCount, price = order.TotalPrice, productDescrp = description, storeName = store.CamexStoreName, address, receiverPhone = phone, notes = BuildNotes(order.Notes) };
        var path = configuration["Camex:CreateShipmentPath"]?.Trim() ?? "/"; if (!path.StartsWith('/')) path = "/" + path;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await CamexToken(ct); if (token is null) return null;
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl("Camex") + "/ApiEndpoints" + path) { Content = JsonContent.Create(payload, options: CourierPayloadOptions) }; request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { cache.Remove("courier:camex:token"); continue; }
            using var json = JsonDocument.Parse(body); if (!response.IsSuccessStatusCode || !json.RootElement.TryGetProperty("type", out var type) || type.GetInt32() != 1) throw new HttpRequestException($"CAMEX returned {(int)response.StatusCode}.");
            if (!json.RootElement.TryGetProperty("content", out var content)) return null; return content.ValueKind == JsonValueKind.Number ? content.GetInt64().ToString() : content.GetString();
        }
        return null;
    }

    public async Task<SandoogOrderDetails> GetSandoogOrderDetailsAsync(string orderId, string? preferredStatus, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orderId)) return new(false, Error: "Missing Sandoog order id.");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await SandoogToken(ct); if (token is null) return new(false, Error: "Sandoog authentication failed.");
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl("Sandoog") + "/orders/" + Uri.EscapeDataString(orderId)); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { cache.Remove("courier:sandoog:token"); continue; }
            if (!response.IsSuccessStatusCode) return new(false, Error: $"Sandoog returned {(int)response.StatusCode}.");
            try
            {
                using var json = JsonDocument.Parse(body); if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0) return new(true);
                JsonElement selected = json.RootElement[json.RootElement.GetArrayLength() - 1];
                if (!string.IsNullOrWhiteSpace(preferredStatus)) foreach (var entry in json.RootElement.EnumerateArray()) if (Read(entry, "status")?.Equals(preferredStatus, StringComparison.OrdinalIgnoreCase) == true) selected = entry;
                return new(true, Read(selected, "status"), Read(selected, "reason"), Read(selected, "reason_code"), Read(selected, "delivery_label"), Read(selected, "fulfillment_id"));
            }
            catch (JsonException exception) { return new(false, Error: exception.Message); }
        }
        return new(false, Error: "Sandoog authorization failed.");
    }

    public async Task<int?> GetCamexStateAsync(long trackingNumber, CancellationToken ct)
    {
        var content = await GetCamexContentAsync("/TrackState?trackNO=" + trackingNumber.ToString(CultureInfo.InvariantCulture), ct); if (!content.HasValue) return null;
        return content.Value.ValueKind == JsonValueKind.Number && content.Value.TryGetInt32(out var number) ? number : content.Value.ValueKind == JsonValueKind.String && int.TryParse(content.Value.GetString(), out number) ? number : null;
    }

    public async Task<(bool Success, int Added, int Updated, int Retired, int Reactivated)> SyncCamexCitiesAsync(CancellationToken ct)
    {
        var content = await GetCamexContentAsync("/Cities", ct); if (!content.HasValue || content.Value.ValueKind != JsonValueKind.Array || content.Value.GetArrayLength() == 0) return default;
        var existing = await context.CamexCities.ToDictionaryAsync(item => item.CamexCityId, ct); var seen = new HashSet<int>(); var now = DateTime.UtcNow; var added = 0; var updated = 0; var retired = 0; var reactivated = 0;
        foreach (var value in content.Value.EnumerateArray())
        {
            if (!value.TryGetProperty("cityId", out var idValue) || !idValue.TryGetInt32(out var id) || id <= 0 || Read(value, "cityName") is not { Length: > 0 } name) continue; seen.Add(id);
            var area = Read(value, "areaName"); var cost = value.TryGetProperty("totalCost", out var costValue) && costValue.TryGetDecimal(out var parsedCost) ? parsedCost : 0m; bool? conversion = value.TryGetProperty("hasConverstion", out var conversionValue) && conversionValue.ValueKind is JsonValueKind.True or JsonValueKind.False ? conversionValue.GetBoolean() : null; int? related = null; if (value.TryGetProperty("releatedId", out var relatedValue)) { if (relatedValue.ValueKind == JsonValueKind.Number && relatedValue.TryGetInt32(out var relatedNumber)) related = relatedNumber; else if (relatedValue.ValueKind == JsonValueKind.String && int.TryParse(relatedValue.GetString(), out relatedNumber)) related = relatedNumber; }
            if (existing.TryGetValue(id, out var city)) { city.CityName = name; city.AreaName = area; city.TotalCost = cost; city.HasConversion = conversion; city.RelatedId = related; city.NormalizedName = NormalizeCity(name); city.LastSyncedAtUtc = now; if (city.IsActive) updated++; else { city.IsActive = true; reactivated++; } }
            else { context.CamexCities.Add(new() { CamexCityId = id, CityName = name, AreaName = area, TotalCost = cost, HasConversion = conversion, RelatedId = related, NormalizedName = NormalizeCity(name), LastSyncedAtUtc = now, IsActive = true }); added++; }
        }
        foreach (var city in existing.Values.Where(item => item.IsActive && !seen.Contains(item.CamexCityId))) { city.IsActive = false; retired++; } await context.SaveChangesAsync(ct); return (true, added, updated, retired, reactivated);
    }

    public async Task<IReadOnlyList<string>?> GetCamexStoresAsync(CancellationToken ct)
    {
        var content = await GetCamexContentAsync("/Stores", ct); return !content.HasValue || content.Value.ValueKind != JsonValueKind.Array ? null : content.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }

    private async Task<JsonElement?> GetCamexContentAsync(string path, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++) { var token = await CamexToken(ct); if (token is null) return null; using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl("Camex") + "/ApiEndpoints" + path); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); var body = await response.Content.ReadAsStringAsync(ct); if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { cache.Remove("courier:camex:token"); continue; } if (!response.IsSuccessStatusCode) return null; using var json = JsonDocument.Parse(body); if (!json.RootElement.TryGetProperty("type", out var type) || type.GetInt32() != 1 || !json.RootElement.TryGetProperty("content", out var content)) return null; return content.Clone(); } return null;
    }

    private async Task<string?> SandoogToken(CancellationToken ct)
    {
        if (cache.TryGetValue<string>("courier:sandoog:token", out var cached)) return cached;
        await SandoogAuthLock.WaitAsync(ct); try { if (cache.TryGetValue<string>("courier:sandoog:token", out cached)) return cached; using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl("Sandoog") + "/auth") { Content = new ByteArrayContent([]) }; request.Headers.TryAddWithoutValidation("Api-Key", configuration["Sandoog:ApiKey"]); using var response = await clients.CreateClient().SendAsync(request, ct); if (!response.IsSuccessStatusCode) return null; using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var token = json.RootElement.GetProperty("access_token").GetString(); var seconds = json.RootElement.TryGetProperty("expires_in", out var expiry) && expiry.TryGetDouble(out var parsed) && parsed > 0 ? parsed : 86400d; var lifetime = TimeSpan.FromSeconds(Math.Max(60d, seconds - 300d)); if (!string.IsNullOrWhiteSpace(token)) cache.Set("courier:sandoog:token", token, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime, Size = 1 }); return token; } finally { SandoogAuthLock.Release(); }
    }

    private async Task<string?> CamexToken(CancellationToken ct)
    {
        if (cache.TryGetValue<string>("courier:camex:token", out var cached)) return cached;
        await CamexAuthLock.WaitAsync(ct); try { if (cache.TryGetValue<string>("courier:camex:token", out cached)) return cached; var url = BaseUrl("Camex") + "/ApiEndpoints/Login?providerKey=" + Uri.EscapeDataString(configuration["Camex:ProviderKey"]!) + "&clientKey=" + Uri.EscapeDataString(configuration["Camex:ClientKey"]!); using var response = await clients.CreateClient().GetAsync(url, ct); if (!response.IsSuccessStatusCode) return null; using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); if (json.RootElement.GetProperty("type").GetInt32() != 1) return null; var content = json.RootElement.GetProperty("content"); var token = content.GetProperty("value").GetString(); var lifetime = TimeSpan.FromMinutes(5); if (content.TryGetProperty("validTo", out var validTo) && DateTimeOffset.TryParse(validTo.GetString(), out var expiry)) lifetime = TimeSpan.FromSeconds(Math.Max(1, (expiry - DateTimeOffset.UtcNow).TotalSeconds - 30)); if (!string.IsNullOrWhiteSpace(token)) cache.Set("courier:camex:token", token, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime, Size = 1 }); return token; } finally { CamexAuthLock.Release(); }
    }

    private static bool IsEligible(string courier, Order order, int companyId) => order.DeliveryCompanyId == companyId && !order.IsHidden && order.OrderStatus == OrderStatusCodes.New && (courier == "sandoog" ? order.SandoogConfirmedAt is null && order.SandoogOrderId is null && !order.SandoogLegacyManual : order.CamexConfirmedAt is null && order.CamexTrackingNumber is null && !order.CamexLegacyManual);
    private async Task<CamexCity?> ResolveCamexCity(string? state, CancellationToken ct) { var key = NormalizeCity(state); if (key.Length == 0) return null; var mapping = await context.CamexCityMappings.AsNoTracking().Include(item => item.CamexCity).FirstOrDefaultAsync(item => item.NormalizedState == key || item.State == state, ct); if (mapping is not null) return mapping.CamexCityId.HasValue && mapping.CamexCity?.IsActive == true ? mapping.CamexCity : null; return await context.CamexCities.AsNoTracking().FirstOrDefaultAsync(item => item.IsActive && item.NormalizedName == key, ct); }
    private string BaseUrl(string section) { var value = (configuration[$"{section}:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/'); return value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? value : "https://" + value; }
    private static string Section(string courier) => courier.Equals("camex", StringComparison.OrdinalIgnoreCase) ? "Camex" : "Sandoog";
    private static string? NormalizeIraqiPhone(string? phone) { var value = Digits(phone); if (value.StartsWith("00964", StringComparison.Ordinal)) value = value[5..]; else if (value.StartsWith("964", StringComparison.Ordinal) && value.Length > 10) value = value[3..]; if (value.Length == 10 && value.StartsWith('7')) value = "0" + value; return value.Length == 11 && value.StartsWith("07", StringComparison.Ordinal) ? value : null; }
    private static string? NormalizeLibyanPhone(string? phone) { var value = Digits(phone); if (value.StartsWith("00218", StringComparison.Ordinal)) value = value[5..]; else if (value.StartsWith("218", StringComparison.Ordinal) && value.Length > 9) value = value[3..]; if (value.Length == 10 && value.StartsWith("09", StringComparison.Ordinal)) value = value[1..]; return value.Length == 9 && value.StartsWith('9') ? value : null; }
    private static string Digits(string? value) { var builder = new StringBuilder(); foreach (var c in value ?? string.Empty) { if (char.IsAsciiDigit(c)) builder.Append(c); else if (c is >= '٠' and <= '٩') builder.Append((char)('0' + c - '٠')); else if (c is >= '۰' and <= '۹') builder.Append((char)('0' + c - '۰')); } return builder.ToString(); }
    private static string BuildNotes(string? staffNote) { const string instruction = "ممنوع فتح الطلب بأي حال من الأحوال"; return string.IsNullOrWhiteSpace(staffNote) ? instruction : staffNote.Trim() + " - " + instruction; }
    private static string? Read(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string NormalizeCity(string? value) { var builder = new StringBuilder(); foreach (var character in (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)) { if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue; var normalized = character switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي', 'ؤ' => 'و', 'ئ' => 'ي', 'ة' => 'ه', 'ـ' => '\0', _ => character }; if (normalized != '\0' && char.IsLetterOrDigit(normalized)) builder.Append(normalized); } return builder.ToString().Normalize(NormalizationForm.FormC); }
}

public sealed class CourierDataException(string message) : Exception(message);
