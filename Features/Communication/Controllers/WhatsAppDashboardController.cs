using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Infrastructure.WhatsApp;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("api/v1/communication/whatsapp")]
[Route("WhatsAppDashboard")]
public sealed class WhatsAppDashboardController(ApplicationDbContext context, LavvaWhatsAppService lavvaService, WhatsAppAutomationService automationService, IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/WhatsAppDashboard/Index")]
    [HttpPost("/WhatsAppDashboard/Index")]
    public async Task<IActionResult> Index(DateTime? logsFrom, DateTime? logsTo, int logsPage = 1, CancellationToken ct = default)
    {
        const int pageSize = 20;
        logsPage = Math.Max(1, logsPage);
        var logsQuery = context.WhatsAppMessages.AsNoTracking();
        if (logsFrom.HasValue) logsQuery = logsQuery.Where(item => item.CreatedAt >= logsFrom.Value.Date);
        if (logsTo.HasValue) logsQuery = logsQuery.Where(item => item.CreatedAt < logsTo.Value.Date.AddDays(1));
        var total = await logsQuery.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        logsPage = Math.Min(logsPage, pages);
        var accounts = await context.WhatsAppAutomationAccounts.AsNoTracking().OrderByDescending(item => item.IsActive).ThenBy(item => item.Name).Select(item => new
        {
            item.Id, item.Name, item.SenderPhoneNumber, item.ProviderType, item.Country, item.ManufacturingCompanyId, item.ApiBaseUrl, item.GreenApiInstanceId,
            hasApiKey = item.ApiKey != null, hasGreenApiToken = item.GreenApiToken != null, item.IsActive, item.CreatedAt, item.UpdatedAt,
            storeIds = item.AccountStores.Select(link => link.ManufacturingCompanyId),
            templates = item.Templates.Select(template => new { template.Id, template.EventType, template.OrderStatus, template.MessageText, template.IsActive })
        }).ToListAsync(ct);
        var globalTemplates = await context.WhatsAppAutomationTemplates.AsNoTracking().Where(item => item.AccountId == null).OrderBy(item => item.EventType).ThenBy(item => item.OrderStatus).ToListAsync(ct);
        var stores = await context.ManufacturingCompanies.AsNoTracking().OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct);
        var logs = await logsQuery.OrderByDescending(item => item.CreatedAt).Skip((logsPage - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { accounts, globalTemplates, logs, logsFrom, logsTo, logsPage, logsPageSize = pageSize, logsTotalCount = total, logsTotalPages = pages, stores });
    }

    [HttpGet("GetMessages")]
    [HttpGet("/WhatsAppDashboard/GetMessages")]
    public async Task<ActionResult<List<WhatsAppMessage>>> GetMessages(string? phone, CancellationToken ct)
    {
        var query = context.WhatsAppMessages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(phone)) query = query.Where(item => item.RecipientPhoneNumber.Contains(phone));
        return Ok(await query.OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(ct));
    }

    [HttpPost("CreateAccount")]
    [HttpPost("/WhatsAppDashboard/CreateAccount")]
    public async Task<IActionResult> CreateAccount([FromBody] WhatsAppAccountRequest request, CancellationToken ct)
    {
        var name = Clean(request.Name);
        if (name.Length == 0) return BadRequest(new { message = "اسم الرقم مطلوب." });
        var storeIds = request.ManufacturingCompanyIds.Where(id => id > 0).Distinct().ToArray();
        var account = new WhatsAppAutomationAccount
        {
            Name = name, SenderPhoneNumber = Clean(request.SenderPhoneNumber), ProviderType = request.ProviderType, Country = request.Country,
            ManufacturingCompanyId = storeIds.Length == 1 ? storeIds[0] : null, ApiBaseUrl = CleanOrNull(request.ApiBaseUrl), ApiKey = CleanOrNull(request.ApiKey),
            GreenApiInstanceId = DigitsOrNull(request.GreenApiInstanceId), GreenApiToken = CleanOrNull(request.GreenApiToken), IsActive = request.IsActive,
            CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId()
        };
        foreach (var storeId in storeIds) account.AccountStores.Add(new WhatsAppAutomationAccountStore { ManufacturingCompanyId = storeId });
        context.WhatsAppAutomationAccounts.Add(account);
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, account.Id });
    }

    [HttpPost("ToggleAccount")]
    [HttpPost("/WhatsAppDashboard/ToggleAccount")]
    public async Task<IActionResult> ToggleAccount([FromQuery] int id, CancellationToken ct)
    {
        var account = await context.WhatsAppAutomationAccounts.FindAsync([id], ct);
        if (account is null) return NotFound();
        account.IsActive = !account.IsActive; Touch(account);
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, account.IsActive });
    }

    [HttpPost("UpdateAccount")]
    [HttpPost("/WhatsAppDashboard/UpdateAccount")]
    public async Task<IActionResult> UpdateAccount([FromBody] WhatsAppAccountRequest request, CancellationToken ct)
    {
        if (!request.Id.HasValue) return NotFound();
        var account = await context.WhatsAppAutomationAccounts.Include(item => item.AccountStores).FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (account is null) return NotFound();
        var name = Clean(request.Name);
        if (name.Length == 0) return BadRequest(new { message = "اسم الرقم مطلوب." });
        var storeIds = request.ManufacturingCompanyIds.Where(id => id > 0).Distinct().ToArray();
        account.Name = name; account.SenderPhoneNumber = Clean(request.SenderPhoneNumber); account.ProviderType = request.ProviderType; account.Country = request.Country;
        account.ManufacturingCompanyId = storeIds.Length == 1 ? storeIds[0] : null; account.ApiBaseUrl = CleanOrNull(request.ApiBaseUrl);
        if (!string.IsNullOrWhiteSpace(request.ApiKey)) account.ApiKey = request.ApiKey.Trim();
        account.GreenApiInstanceId = DigitsOrNull(request.GreenApiInstanceId);
        if (!string.IsNullOrWhiteSpace(request.GreenApiToken)) account.GreenApiToken = request.GreenApiToken.Trim();
        account.IsActive = request.IsActive; Touch(account);
        context.WhatsAppAutomationAccountStores.RemoveRange(account.AccountStores);
        account.AccountStores = storeIds.Select(storeId => new WhatsAppAutomationAccountStore { AccountId = account.Id, ManufacturingCompanyId = storeId }).ToList();
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("DeleteAccount")]
    [HttpPost("/WhatsAppDashboard/DeleteAccount")]
    public async Task<IActionResult> DeleteAccount([FromQuery] int id, CancellationToken ct)
    {
        var account = await context.WhatsAppAutomationAccounts.FindAsync([id], ct);
        if (account is null) return NotFound();
        var templateIds = await context.WhatsAppAutomationTemplates.Where(item => item.AccountId == id).Select(item => item.Id).ToArrayAsync(ct);
        await context.WhatsAppMessages.Where(item => item.AccountId == id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AccountId, (int?)null), ct);
        if (templateIds.Length > 0) await context.WhatsAppMessages.Where(item => item.TemplateId.HasValue && templateIds.Contains(item.TemplateId.Value)).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.TemplateId, (int?)null), ct);
        await context.WhatsAppAutomationTemplates.Where(item => item.AccountId == id).ExecuteDeleteAsync(ct);
        context.WhatsAppAutomationAccounts.Remove(account);
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("CreateTemplate")]
    [HttpPost("/WhatsAppDashboard/CreateTemplate")]
    public async Task<IActionResult> CreateTemplate([FromBody] WhatsAppTemplateRequest request, CancellationToken ct)
    {
        var error = await ValidateTemplate(request, ct); if (error is not null) return BadRequest(new { message = error });
        var item = new WhatsAppAutomationTemplate { AccountId = request.AccountId, EventType = 2, OrderStatus = request.OrderStatus, MessageText = request.MessageText!.Trim(), IsActive = request.IsActive, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId() };
        context.WhatsAppAutomationTemplates.Add(item); await context.SaveChangesAsync(ct);
        return Ok(new { success = true, item.Id });
    }

    [HttpPost("ToggleTemplate")]
    [HttpPost("/WhatsAppDashboard/ToggleTemplate")]
    public async Task<IActionResult> ToggleTemplate([FromQuery] int id, CancellationToken ct)
    {
        var item = await context.WhatsAppAutomationTemplates.FindAsync([id], ct); if (item is null) return NotFound();
        item.IsActive = !item.IsActive; item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); await context.SaveChangesAsync(ct);
        return Ok(new { success = true, item.IsActive });
    }

    [HttpPost("UpdateTemplate")]
    [HttpPost("/WhatsAppDashboard/UpdateTemplate")]
    public async Task<IActionResult> UpdateTemplate([FromBody] WhatsAppTemplateRequest request, CancellationToken ct)
    {
        if (!request.Id.HasValue) return NotFound();
        var item = await context.WhatsAppAutomationTemplates.FindAsync([request.Id.Value], ct); if (item is null) return NotFound();
        var error = await ValidateTemplate(request, ct); if (error is not null) return BadRequest(new { message = error });
        item.EventType = 2; item.OrderStatus = request.OrderStatus; item.MessageText = request.MessageText!.Trim(); item.IsActive = request.IsActive; item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId();
        await context.SaveChangesAsync(ct); return Ok(new { success = true });
    }

    [HttpPost("DeleteTemplate")]
    [HttpPost("/WhatsAppDashboard/DeleteTemplate")]
    public async Task<IActionResult> DeleteTemplate([FromQuery] int id, CancellationToken ct)
    {
        var item = await context.WhatsAppAutomationTemplates.FindAsync([id], ct); if (item is null) return NotFound();
        await context.WhatsAppMessages.Where(log => log.TemplateId == id).ExecuteUpdateAsync(setters => setters.SetProperty(log => log.TemplateId, (int?)null), ct);
        context.WhatsAppAutomationTemplates.Remove(item); await context.SaveChangesAsync(ct); return Ok(new { success = true });
    }

    [HttpGet("GreenApiState")]
    [HttpGet("/WhatsAppDashboard/GreenApiState")]
    public Task<IActionResult> GreenApiState([FromQuery] int id, CancellationToken ct) => GreenApiGet(id, "getStateInstance", "state", ct);

    [HttpGet("GreenApiQr")]
    [HttpGet("/WhatsAppDashboard/GreenApiQr")]
    public Task<IActionResult> GreenApiQr([FromQuery] int id, CancellationToken ct) => GreenApiGet(id, "qr", "qr", ct);

    [HttpPost("GreenApiLogout")]
    [HttpPost("/WhatsAppDashboard/GreenApiLogout")]
    public Task<IActionResult> GreenApiLogout([FromQuery] int id, CancellationToken ct) => GreenApiGet(id, "logout", "logout", ct);

    [HttpPost("send")]
    [HttpPost("SendMessage")]
    [HttpPost("/WhatsAppDashboard/SendMessage")]
    public async Task<ActionResult<WhatsAppMessage>> SendWhatsApp([FromBody] SendWhatsAppRequest request, CancellationToken ct)
    {
        var sent = await automationService.SendOrderAlertAsync(request.OrderId ?? 0, request.PhoneNumber, request.Message, ct);
        var item = new WhatsAppMessage { OrderId = request.OrderId, RecipientPhoneNumber = request.PhoneNumber, EventType = request.OrderId.HasValue ? 2 : 1, Success = sent, ErrorMessage = sent ? null : "Provider did not confirm the send operation.", CreatedAt = IstanbulTimeHelper.Now };
        context.WhatsAppMessages.Add(item); await context.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("send-failed-delivery")]
    [HttpPost("/WhatsAppDashboard/SendFailedDelivery")]
    public async Task<IActionResult> SendFailedDelivery([FromBody] LavvaFailedDeliveryWhatsAppRequest request, CancellationToken ct) => Ok(await lavvaService.SendFailedDeliveryAsync(request, ct));

    private async Task<string?> ValidateTemplate(WhatsAppTemplateRequest request, CancellationToken ct)
    {
        if (request.AccountId.HasValue && !await context.WhatsAppAutomationAccounts.AnyAsync(item => item.Id == request.AccountId, ct)) return "رقم الواتساب غير موجود.";
        if (string.IsNullOrWhiteSpace(request.MessageText)) return "نص الرسالة مطلوب.";
        return request.OrderStatus.HasValue ? null : "اختار حالة الطلب للرسالة.";
    }

    private async Task<IActionResult> GreenApiGet(int id, string method, string responseKind, CancellationToken ct)
    {
        var account = await context.WhatsAppAutomationAccounts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (!TryGreenEndpoint(account, method, out var endpoint, out var error)) return BadRequest(new { ok = false, message = error });
        try
        {
            using var response = await httpClientFactory.CreateClient().GetAsync(endpoint, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, new { ok = false, message = $"Green API رجع خطأ {(int)response.StatusCode}." });
            using var json = JsonDocument.Parse(body);
            if (responseKind == "state") { var state = ReadString(json, "stateInstance"); return Ok(new { ok = true, state, label = GreenStateLabel(state) }); }
            if (responseKind == "qr") { var type = ReadString(json, "type"); var value = ReadString(json, "message"); return Ok(new { ok = true, type, message = value, imageDataUrl = type == "qrCode" && value != null ? "data:image/png;base64," + value : null }); }
            var loggedOut = json.RootElement.TryGetProperty("isLogout", out var property) && property.ValueKind is JsonValueKind.True;
            return Ok(new { ok = loggedOut, isLogout = loggedOut });
        }
        catch (Exception ex) { return StatusCode(502, new { ok = false, message = "تعذر الاتصال بـ Green API: " + ex.Message }); }
    }

    private static bool TryGreenEndpoint(WhatsAppAutomationAccount? account, string method, out string endpoint, out string error)
    {
        endpoint = error = string.Empty;
        if (account is null) { error = "رقم الواتساب غير موجود."; return false; }
        if (account.ProviderType != 2) { error = "الرقم ليس Green API."; return false; }
        var id = DigitsOrNull(account.GreenApiInstanceId); var token = CleanOrNull(account.GreenApiToken);
        if (id is null || token is null) { error = "Green API Instance ID أو Token غير موجود."; return false; }
        var host = CleanOrNull(account.ApiBaseUrl) ?? "https://api.green-api.com";
        if (!Uri.TryCreate(host, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) { error = "API Base URL غير صحيح."; return false; }
        endpoint = $"{uri.GetLeftPart(UriPartial.Authority)}/waInstance{id}/{method}/{Uri.EscapeDataString(token)}"; return true;
    }

    private void Touch(WhatsAppAutomationAccount item) { item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); }
    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
    private static string? CleanOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? DigitsOrNull(string? value) { var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray()); return digits.Length == 0 ? null : digits; }
    private static string? ReadString(JsonDocument json, string name) => json.RootElement.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static string GreenStateLabel(string? state) => state switch { "authorized" => "متصل ومصرح", "notAuthorized" => "غير مربوط", "blocked" => "محظور", "sleepMode" => "Sleep mode", "starting" => "جاري التشغيل", "yellowCard" => "إرسال الرسائل موقوف مؤقتا", "pendingCode" => "في انتظار كود التأكيد", _ => state ?? "حالة غير معروفة" };
}

public sealed record WhatsAppAccountRequest(int? Id, string? Name, string? SenderPhoneNumber, int ProviderType, int? Country, int[] ManufacturingCompanyIds, string? ApiBaseUrl, string? ApiKey, string? GreenApiInstanceId, string? GreenApiToken, bool IsActive = true);
public sealed record WhatsAppTemplateRequest(int? Id, int? AccountId, int? OrderStatus, string? MessageText, bool IsActive = true);
public sealed record SendWhatsAppRequest(string PhoneNumber, string Message, int? OrderId);
