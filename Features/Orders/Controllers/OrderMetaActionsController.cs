using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Luxira.Api.Utils.Time;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/{id:int}/meta")]
[Route("Order")]
public class OrderMetaActionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<OrderHub> _hub;
    private static readonly HashSet<string> AllowedReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "معالجة طلب", "موافقة", "متابعة توصيل", "متابعة التوصيل", "حل مشكلة",
        "التأكد من الاستلام", "الطلبات الغير مكتمله", "تشيك المعلومات", "أخرى"
    };

    public OrderMetaActionsController(ApplicationDbContext context, IHubContext<OrderHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    [HttpPost("pin")]
    [HttpPost("/Order/PinOrder/{id:int}")]
    public async Task<IActionResult> TogglePin([RouteOrRequest] int id, CancellationToken ct)
    {
        var order = await _context.Orders.FindAsync([id], ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {id} not found.");
        }

        order.IsPinned = !order.IsPinned;
        order.PinnedAt = order.IsPinned ? DateTime.UtcNow : null;
        order.PinnedByUserId = order.IsPinned ? User.GetUserId() : null;

        await _context.SaveChangesAsync(ct);
        return Ok(new { isPinned = order.IsPinned });
    }

    [HttpPost("delayed")]
    [HttpPost("/Order/SetDelayed/{id:int}")]
    public async Task<IActionResult> ToggleDelayed([RouteOrRequest] int id, [FromBody] SetDelayedRequest? request, CancellationToken ct)
    {
        var order = await _context.Orders.FindAsync([id], ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {id} not found.");
        }

        order.IsDelayed = request?.IsDelayed ?? !order.IsDelayed;
        order.LastEditedDate = DateTime.UtcNow;
        order.Editedby = User.GetUserId();

        await _context.SaveChangesAsync(ct);
        return Ok(new { isDelayed = order.IsDelayed });
    }

    [HttpPost("special-client")]
    [HttpPost("/Order/SetSpecialClient/{id:int}")]
    public async Task<IActionResult> ToggleSpecialClient([RouteOrRequest] int id, CancellationToken ct)
    {
        var order = await _context.Orders.FindAsync([id], ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {id} not found.");
        }

        order.IsClientSpecial = !order.IsClientSpecial;
        await _context.SaveChangesAsync(ct);
        return Ok(new { isClientSpecial = order.IsClientSpecial });
    }

    [HttpPost("/OrderMetaActions/Save")]
    public async Task<IActionResult> Save([FromBody] SaveMetaActionRequest request, CancellationToken ct)
    {
        if (request.OrderId <= 0) return Ok(new { success = false, message = "لم يتم التعرف على رقم الطلب." });
        var reason = (request.Reason ?? string.Empty).Trim();
        var other = (request.OtherText ?? string.Empty).Trim();
        if (!AllowedReasons.Contains(reason)) return Ok(new { success = false, message = "اختاري سبب صحيح لفتح رابط ميتا." });
        if (reason == "أخرى" && other.Length == 0) return Ok(new { success = false, message = "اكتبي السبب في خانة أخرى." });
        if (reason is "موافقة" or "متابعة توصيل") reason = "متابعة التوصيل";
        var userId = User.GetUserId();
        var employeeName = userId is null ? null : await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => employee.DisplayName ?? employee.Name)
            .FirstOrDefaultAsync(ct);
        var click = new OrderMetaActionClick
        {
            OrderId = request.OrderId,
            UserId = userId,
            EmployeeName = employeeName ?? User.Identity?.Name ?? "موظف",
            Reason = reason,
            OtherText = other.Length == 0 ? null : other[..Math.Min(other.Length, 500)],
            MetaUrl = string.IsNullOrWhiteSpace(request.Url) ? null : request.Url.Trim(),
            ContactType = NormalizeContactType(request.ContactType, request.Url),
            ClickedAt = IstanbulTimeHelper.Now
        };
        _context.OrderMetaActionClicks.Add(click);
        await _context.SaveChangesAsync(ct);
        var item = await BuildSummariesAsync([request.OrderId], ct);
        await _hub.Clients.All.SendAsync("OrderMetaActionSummaryChanged", new { orderId = request.OrderId, item = item.FirstOrDefault() }, ct);
        return Ok(new { success = true, item = item.FirstOrDefault() });
    }

    [HttpGet("/OrderMetaActions/Summary")]
    public async Task<IActionResult> Summary([FromQuery] string? orderIds, CancellationToken ct)
    {
        var ids = (orderIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).Distinct().Take(300).ToArray();
        return Ok(new { success = true, items = ids.Length == 0 ? [] : await BuildSummariesAsync(ids, ct) });
    }

    [HttpGet("/OrderMetaActions/RatingSummary")]
    public async Task<IActionResult> RatingSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct)
    {
        var query = Scoped().Where(click => (!startDate.HasValue || click.ClickedAt >= startDate) && (!endDate.HasValue || click.ClickedAt < endDate));
        var grouped = await query.GroupBy(click => new { click.UserId, click.EmployeeName, Reason = click.Reason == "موافقة" ? "متابعة التوصيل" : click.Reason })
            .Select(group => new { group.Key.UserId, group.Key.EmployeeName, group.Key.Reason, Count = group.Count() }).ToListAsync(ct);
        var userIds = grouped.Where(row => row.UserId != null).Select(row => row.UserId!).Distinct().ToArray();
        var employees = await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId != null && userIds.Contains(employee.ApplicationUserId))
            .Select(employee => new { employee.ApplicationUserId, employee.Id, Name = employee.DisplayName ?? employee.Name }).ToDictionaryAsync(employee => employee.ApplicationUserId!, ct);
        var items = grouped.Select(row => new
        {
            employeeId = row.UserId is not null && employees.TryGetValue(row.UserId, out var employee) ? employee.Id.ToString() : string.Empty,
            userId = row.UserId ?? string.Empty,
            employeeName = row.UserId is not null && employees.TryGetValue(row.UserId, out employee) ? employee.Name : row.EmployeeName ?? "موظف",
            reason = NormalizeReason(row.Reason),
            count = row.Count
        });
        return Ok(new { success = true, items });
    }

    [HttpGet("/OrderMetaActions/RatingDetails")]
    public async Task<IActionResult> RatingDetails([FromQuery] string? employeeId, [FromQuery] string? employeeName, [FromQuery] string? reason, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken ct)
    {
        reason = NormalizeReason(reason);
        if (!AllowedReasons.Contains(reason)) return Ok(new { success = false, message = "نوع الإجراء غير صحيح." });
        string? selectedUserId = null;
        if (int.TryParse(employeeId, out var parsedEmployeeId))
            selectedUserId = await _context.Employees.AsNoTracking().Where(employee => employee.Id == parsedEmployeeId).Select(employee => employee.ApplicationUserId).FirstOrDefaultAsync(ct);
        selectedUserId ??= employeeId;
        var query = Scoped().Where(click =>
            (click.Reason == "موافقة" || click.Reason == "متابعة توصيل" ? "متابعة التوصيل" : click.Reason) == reason &&
            (!startDate.HasValue || click.ClickedAt >= startDate) &&
            (!endDate.HasValue || click.ClickedAt < endDate));
        if (!string.IsNullOrWhiteSpace(selectedUserId)) query = query.Where(click => click.UserId == selectedUserId || click.EmployeeName == employeeName);
        return Ok(new { success = true, items = await ProjectLogs(query).Take(1000).ToListAsync(ct) });
    }

    [HttpGet("/OrderMetaActions/ReasonStats")]
    public async Task<IActionResult> ReasonStats([FromQuery] string? contactType, [FromQuery] int? orderId, [FromQuery] string? url, CancellationToken ct)
    {
        var normalizedContact = NormalizeContactTypeFilter(contactType);
        if (!orderId.HasValue || orderId <= 0) return Ok(new { success = true, contactType = normalizedContact.Length == 0 ? "All" : normalizedContact, orderId = 0, items = Array.Empty<object>() });
        var query = ApplyMetaFilters(Scoped().Where(click => click.OrderId == orderId), normalizedContact, url);
        var logs = await ProjectLogs(query).Take(500).ToListAsync(ct);
        string[] reasons = ["معالجة طلب", "متابعة التوصيل", "حل مشكلة", "التأكد من الاستلام", "الطلبات الغير مكتمله", "تشيك المعلومات", "أخرى"];
        var items = reasons.Select(reason => new { reason, count = logs.Count(log => NormalizeReason(log.reason) == reason), logs = logs.Where(log => NormalizeReason(log.reason) == reason).Take(25) });
        return Ok(new { success = true, contactType = normalizedContact.Length == 0 ? "All" : normalizedContact, orderId, items });
    }

    [HttpGet("/OrderMetaActions/AllLogs")]
    public async Task<IActionResult> AllLogs([FromQuery] string? contactType, [FromQuery] int? orderId, [FromQuery] string? url, CancellationToken ct)
    {
        var normalizedContact = NormalizeContactTypeFilter(contactType);
        if (!orderId.HasValue || orderId <= 0) return Ok(new { success = true, contactType = normalizedContact.Length == 0 ? "All" : normalizedContact, orderId = 0, items = Array.Empty<object>() });
        var items = await ProjectLogs(ApplyMetaFilters(Scoped().Where(click => click.OrderId == orderId), normalizedContact, url)).Take(1000).ToListAsync(ct);
        return Ok(new { success = true, contactType = normalizedContact.Length == 0 ? "All" : normalizedContact, orderId, items });
    }

    private IQueryable<OrderMetaActionClick> Scoped()
    {
        var query = _context.OrderMetaActionClicks.AsNoTracking();
        return User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector") || User.IsInRole("FollowUpDepartment")
            ? query : query.Where(click => click.UserId == User.GetUserId());
    }

    private async Task<List<object>> BuildSummariesAsync(int[] ids, CancellationToken ct)
    {
        var counts = await Scoped().Where(click => ids.Contains(click.OrderId)).GroupBy(click => click.OrderId)
            .Select(group => new
            {
                OrderId = group.Key,
                Count = group.Count(),
                WhatsApp = group.Count(click => click.ContactType == "WhatsApp" || (click.MetaUrl != null && (click.MetaUrl.Contains("whatsapp") || click.MetaUrl.Contains("wa.me"))))
            }).ToListAsync(ct);
        return ids.Select(id =>
        {
            var count = counts.FirstOrDefault(item => item.OrderId == id);
            return (object)new { orderId = id, count = count?.Count ?? 0, metaCount = (count?.Count ?? 0) - (count?.WhatsApp ?? 0), whatsappCount = count?.WhatsApp ?? 0, logs = Array.Empty<object>() };
        }).ToList();
    }

    private static IQueryable<OrderMetaActionClick> ApplyMetaFilters(IQueryable<OrderMetaActionClick> query, string contactType, string? url)
    {
        if (contactType == "WhatsApp") query = query.Where(click => click.ContactType == "WhatsApp" || (click.MetaUrl != null && (click.MetaUrl.Contains("whatsapp") || click.MetaUrl.Contains("wa.me"))));
        else if (contactType == "Meta") query = query.Where(click => click.ContactType != "WhatsApp" && (click.MetaUrl == null || (!click.MetaUrl.Contains("whatsapp") && !click.MetaUrl.Contains("wa.me"))));
        if (!string.IsNullOrWhiteSpace(url)) query = query.Where(click => click.MetaUrl == url.Trim());
        return query;
    }

    private static IQueryable<MetaLogDto> ProjectLogs(IQueryable<OrderMetaActionClick> query) => query
        .OrderByDescending(click => click.ClickedAt).ThenByDescending(click => click.Id)
        .Select(click => new MetaLogDto(click.OrderId, click.EmployeeName ?? "موظف", click.Reason, click.OtherText ?? string.Empty, click.MetaUrl ?? string.Empty, click.ContactType ?? "Meta", click.ClickedAt));

    private static string NormalizeContactType(string? contactType, string? url) => NormalizeContactTypeFilter($"{contactType} {url}") == "WhatsApp" ? "WhatsApp" : "Meta";
    private static string NormalizeContactTypeFilter(string? value) => (value ?? string.Empty).Contains("whatsapp", StringComparison.OrdinalIgnoreCase) || (value ?? string.Empty).Contains("wa.me", StringComparison.OrdinalIgnoreCase) || (value ?? string.Empty).Contains("واتساب", StringComparison.OrdinalIgnoreCase) ? "WhatsApp" : (value ?? string.Empty).Contains("meta", StringComparison.OrdinalIgnoreCase) || (value ?? string.Empty).Contains("ميتا", StringComparison.OrdinalIgnoreCase) ? "Meta" : string.Empty;
    private static string NormalizeReason(string? reason) => reason is "موافقة" or "متابعة توصيل" ? "متابعة التوصيل" : (reason ?? string.Empty).Trim();
}

public record SetDelayedRequest(bool IsDelayed, string? Reason);
public sealed record SaveMetaActionRequest(int OrderId, string? Reason, string? OtherText, string? Url, string? ContactType);
public sealed record MetaLogDto(int orderId, string employeeName, string reason, string otherText, string metaUrl, string contactType, DateTime clickedAt);
