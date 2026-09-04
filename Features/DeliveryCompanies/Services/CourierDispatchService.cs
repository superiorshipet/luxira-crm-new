using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Luxira.Api.Features.DeliveryCompanies.Services;

public enum CourierConfirmOutcome { Sent, Blocked, Deferred }
public sealed record CourierConfirmResult(CourierConfirmOutcome Outcome, string Message, string? ExternalReference = null);

public sealed class CourierDispatchService(ApplicationDbContext context, IConfiguration configuration, IHttpClientFactory clients, IMemoryCache cache, ILogger<CourierDispatchService> logger)
{
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
        var mapping = await context.CamexCityMappings.AsNoTracking().Include(item => item.CamexCity).FirstOrDefaultAsync(item => item.State == order.State && item.CamexCityId != null, ct);
        if (mapping?.CamexCity is null) return "غير مرتبطة بمدينة لدى كامكس";
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
        var token = await SandoogToken(ct); if (token is null) return null;
        var state = order.State?.Trim() ?? string.Empty; if (!SandoogCities.TryGetValue(state, out var city)) throw new CourierDataException("مدينة الطلب غير مرتبطة بمحافظة لدى صندوق.");
        var phone = NormalizeIraqiPhone(order.TelephoneNumber) ?? throw new CourierDataException("رقم الهاتف غير صالح لصندوق.");
        var items = order.OrderWarehouses.Where(line => line.Amount > 0 && !string.IsNullOrWhiteSpace(line.Warehouse?.SubWarehouse?.ProductCode)).Select(line => new { sku = line.Warehouse!.SubWarehouse!.ProductCode, quantity = line.Amount }).ToList();
        if (items.Count == 0) throw new CourierDataException("لا توجد أصناف مسجلة برمز منتج صالح لصندوق.");
        var entity = order.ManufacturingCompanyId.HasValue ? configuration[$"Sandoog:EntityIdByStore:{order.ManufacturingCompanyId}"] : null; entity = string.IsNullOrWhiteSpace(entity) ? configuration["Sandoog:EntityId"] : entity;
        var payload = new { customer = new { name = order.CustomerName, phone, state = city, address = order.Address, second_phone = NormalizeIraqiPhone(order.SecondTelephoneNumber) }, delivery = new { delivery_type = configuration["Sandoog:DeliveryType"] ?? "Standard", delivery_region = "Center", delivery_items = items }, payment = new { total_price = order.TotalPrice, payment_charge_type = configuration["Sandoog:PaymentChargeType"] ?? "Customer", amount_include_delivery_charge = true }, entity_id = entity, external_reference = order.Id.ToString(), notes = BuildNotes(order.Notes) };
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl("Sandoog") + "/orders") { Content = JsonContent.Create(payload) }; request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) { if (body.Contains("stock", StringComparison.OrdinalIgnoreCase) || body.Contains("quantity", StringComparison.OrdinalIgnoreCase)) throw new CourierDataException(body); throw new HttpRequestException($"Sandoog returned {(int)response.StatusCode}."); }
        using var json = JsonDocument.Parse(body); return json.RootElement.ValueKind == JsonValueKind.String ? json.RootElement.GetString() : json.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private async Task<string?> SendCamex(Order order, CancellationToken ct)
    {
        if (!(configuration.GetValue<bool?>("Camex:Enabled") ?? false)) return null;
        var city = await context.CamexCityMappings.AsNoTracking().Include(item => item.CamexCity).FirstOrDefaultAsync(item => item.State == order.State && item.CamexCityId != null, ct) ?? throw new CourierDataException("مدينة الطلب غير مرتبطة بكامكس.");
        var store = await context.CamexStoreMappings.AsNoTracking().FirstOrDefaultAsync(item => item.ManufacturingCompanyId == order.ManufacturingCompanyId && item.CamexStoreName != null, ct) ?? throw new CourierDataException("المتجر غير مرتبط بكامكس.");
        var lines = await context.OrderWarehouses.AsNoTracking().Where(item => item.OrderId == order.Id && item.Amount > 0).Join(context.Warehouses.AsNoTracking(), line => line.WarehouseId, warehouse => warehouse.Id, (line, warehouse) => new { line.Amount, warehouse.Name }).ToListAsync(ct);
        if (lines.Count == 0) throw new CourierDataException("الطلب لا يحتوي على منتجات.");
        var description = string.Join("، ", lines.Select(line => $"{line.Name} x{line.Amount}")); if (description.Length > 80) description = description[..80];
        var payload = new { cityId = city.CamexCityId, areaName = city.CamexCity?.AreaName, noItems = lines.Sum(line => line.Amount), price = order.TotalPrice, productDescrp = description, storeName = store.CamexStoreName, address = order.Address.Trim(), receiverPhone = NormalizeLibyanPhone(order.TelephoneNumber), notes = BuildNotes(order.Notes) };
        var token = await CamexToken(ct); if (token is null) return null;
        var path = configuration["Camex:CreateShipmentPath"]?.Trim() ?? "/"; if (!path.StartsWith('/')) path = "/" + path;
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl("Camex") + "/ApiEndpoints" + path) { Content = JsonContent.Create(payload) }; request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await clients.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); var body = await response.Content.ReadAsStringAsync(ct); using var json = JsonDocument.Parse(body);
        if (!response.IsSuccessStatusCode || !json.RootElement.TryGetProperty("type", out var type) || type.GetInt32() != 1) throw new HttpRequestException($"CAMEX returned {(int)response.StatusCode}.");
        if (!json.RootElement.TryGetProperty("content", out var content)) return null; return content.ValueKind == JsonValueKind.Number ? content.GetInt64().ToString() : content.GetString();
    }

    private async Task<string?> SandoogToken(CancellationToken ct)
    {
        if (cache.TryGetValue<string>("courier:sandoog:token", out var cached)) return cached;
        await SandoogAuthLock.WaitAsync(ct); try { if (cache.TryGetValue<string>("courier:sandoog:token", out cached)) return cached; using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl("Sandoog") + "/auth") { Content = new ByteArrayContent([]) }; request.Headers.TryAddWithoutValidation("Api-Key", configuration["Sandoog:ApiKey"]); using var response = await clients.CreateClient().SendAsync(request, ct); if (!response.IsSuccessStatusCode) return null; using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var token = json.RootElement.GetProperty("access_token").GetString(); if (!string.IsNullOrWhiteSpace(token)) cache.Set("courier:sandoog:token", token, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12), Size = 1 }); return token; } finally { SandoogAuthLock.Release(); }
    }

    private async Task<string?> CamexToken(CancellationToken ct)
    {
        if (cache.TryGetValue<string>("courier:camex:token", out var cached)) return cached;
        await CamexAuthLock.WaitAsync(ct); try { if (cache.TryGetValue<string>("courier:camex:token", out cached)) return cached; var url = BaseUrl("Camex") + "/ApiEndpoints/Login?providerKey=" + Uri.EscapeDataString(configuration["Camex:ProviderKey"]!) + "&clientKey=" + Uri.EscapeDataString(configuration["Camex:ClientKey"]!); using var response = await clients.CreateClient().GetAsync(url, ct); if (!response.IsSuccessStatusCode) return null; using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); if (json.RootElement.GetProperty("type").GetInt32() != 1) return null; var token = json.RootElement.GetProperty("content").GetProperty("value").GetString(); if (!string.IsNullOrWhiteSpace(token)) cache.Set("courier:camex:token", token, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30), Size = 1 }); return token; } finally { CamexAuthLock.Release(); }
    }

    private static bool IsEligible(string courier, Order order, int companyId) => order.DeliveryCompanyId == companyId && !order.IsHidden && order.OrderStatus == OrderStatusCodes.New && (courier == "sandoog" ? order.SandoogConfirmedAt is null && order.SandoogOrderId is null && !order.SandoogLegacyManual : order.CamexConfirmedAt is null && order.CamexTrackingNumber is null && !order.CamexLegacyManual);
    private string BaseUrl(string section) { var value = (configuration[$"{section}:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/'); return value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? value : "https://" + value; }
    private static string Section(string courier) => courier.Equals("camex", StringComparison.OrdinalIgnoreCase) ? "Camex" : "Sandoog";
    private static string? NormalizeIraqiPhone(string? phone) { var value = Digits(phone); if (value.StartsWith("00964", StringComparison.Ordinal)) value = value[5..]; else if (value.StartsWith("964", StringComparison.Ordinal) && value.Length > 10) value = value[3..]; if (value.Length == 10 && value.StartsWith('7')) value = "0" + value; return value.Length == 11 && value.StartsWith("07", StringComparison.Ordinal) ? value : null; }
    private static string? NormalizeLibyanPhone(string? phone) { var value = Digits(phone); if (value.StartsWith("00218", StringComparison.Ordinal)) value = value[5..]; else if (value.StartsWith("218", StringComparison.Ordinal) && value.Length > 9) value = value[3..]; if (value.Length == 10 && value.StartsWith("09", StringComparison.Ordinal)) value = value[1..]; return value.Length == 9 && value.StartsWith('9') ? value : null; }
    private static string Digits(string? value) { var builder = new StringBuilder(); foreach (var c in value ?? string.Empty) { if (char.IsAsciiDigit(c)) builder.Append(c); else if (c is >= '٠' and <= '٩') builder.Append((char)('0' + c - '٠')); else if (c is >= '۰' and <= '۹') builder.Append((char)('0' + c - '۰')); } return builder.ToString(); }
    private static string BuildNotes(string? staffNote) { const string instruction = "ممنوع فتح الطلب بأي حال من الأحوال"; return string.IsNullOrWhiteSpace(staffNote) ? instruction : staffNote.Trim() + " - " + instruction; }
}

public sealed class CourierDataException(string message) : Exception(message);
