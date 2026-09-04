using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

public partial class OrderController
{
    [HttpPost("/Order/RecordOrderDetailsFieldCopy")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter,DeliveryCompany,DeliveryRepresentative")]
    public async Task<IActionResult> RecordOrderDetailsFieldCopy([FromForm] int orderId, [FromForm] string fieldKey, [FromForm] string fieldLabel, [FromForm] string copiedValue, [FromForm] string? sourcePageName, CancellationToken ct)
    {
        if (!await _context.Orders.AsNoTracking().AnyAsync(order => order.Id == orderId, ct)) return NotFound(new { success = false });
        if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(copiedValue)) return BadRequest(new { success = false, message = "بيانات النسخ غير مكتملة." });
        _context.OrderDetailsFieldAuditLogs.Add(new OrderDetailsFieldAuditLog
        {
            OrderId = orderId, ActionType = "Copy", FieldKey = Limit(fieldKey, 120)!, FieldLabel = Limit(string.IsNullOrWhiteSpace(fieldLabel) ? fieldKey : fieldLabel, 250)!,
            CopiedValue = copiedValue, SourcePageName = Limit(sourcePageName, 250), CreatedAt = IstanbulTimeHelper.Now,
            CreatedByUserId = User.GetUserId(), CreatedByUserName = await CurrentAuditUserName(ct)
        });
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("/Order/GetOrderDetailsFieldAuditSummary")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetOrderDetailsFieldAuditSummary([FromQuery] int orderId, CancellationToken ct)
    {
        var rows = await _context.OrderDetailsFieldAuditLogs.AsNoTracking().Where(item => item.OrderId == orderId)
            .GroupBy(item => new { item.FieldKey, item.FieldLabel }).Select(group => new
            {
                fieldKey = group.Key.FieldKey, fieldLabel = group.Key.FieldLabel,
                copyCount = group.Count(item => item.ActionType == "Copy"), editCount = group.Count(item => item.ActionType == "Edit")
            }).ToListAsync(ct);
        return Ok(new { success = true, fields = rows });
    }

    [HttpGet("/Order/GetOrderDetailsFieldAuditRows")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetOrderDetailsFieldAuditRows([FromQuery] int orderId, [FromQuery] string fieldKey, [FromQuery] string actionType, CancellationToken ct)
    {
        var normalizedAction = actionType.Equals("Copy", StringComparison.OrdinalIgnoreCase) ? "Copy" : "Edit";
        if (normalizedAction == "Copy" && !User.IsInRole("Admin") && !User.IsInRole("Administrator")) return Forbid();
        var rows = await _context.OrderDetailsFieldAuditLogs.AsNoTracking().Where(item => item.OrderId == orderId && item.FieldKey == fieldKey && item.ActionType == normalizedAction)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(300)
            .Select(item => new { item.Id, item.FieldLabel, item.OldValue, item.NewValue, item.ChangeReason, item.CopiedValue, item.SourcePageName, userName = item.CreatedByUserName, item.CreatedAt }).ToListAsync(ct);
        return Ok(new { success = true, rows });
    }

    [HttpPost("/Order/RecordOrderContentView")]
    public async Task<IActionResult> RecordOrderContentView([FromForm] int orderId, [FromForm] string contentType, [FromForm] string contentKey, [FromForm] string? contentLabel, [FromForm] string? sourcePageName, CancellationToken ct)
    {
        if (!await _context.Orders.AsNoTracking().AnyAsync(order => order.Id == orderId, ct)) return NotFound(new { success = false });
        _context.OrderContentViewLogs.Add(new OrderContentViewLog
        {
            OrderId = orderId, ContentType = Normalize(contentType, 80, "OrderContent"), ContentKey = Normalize(contentKey, 500, "default"),
            ContentLabel = Normalize(contentLabel, 250, contentType), SourcePageName = Normalize(sourcePageName, 250, "تفاصيل الطلب"),
            ViewedAt = IstanbulTimeHelper.Now, ViewedByUserId = User.GetUserId(), ViewedByUserName = await CurrentAuditUserName(ct)
        });
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("/Order/GetOrderContentViewers")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetOrderContentViewers([FromQuery] int orderId, [FromQuery] string contentType, [FromQuery] string contentKey, CancellationToken ct)
    {
        contentType = Normalize(contentType, 80, "OrderContent"); contentKey = Normalize(contentKey, 500, "default");
        var rows = await _context.OrderContentViewLogs.AsNoTracking().Where(item => item.OrderId == orderId && item.ContentType == contentType && item.ContentKey == contentKey)
            .GroupBy(item => new { item.ViewedByUserId, item.ViewedByUserName }).Select(group => new
            {
                userId = group.Key.ViewedByUserId, userName = group.Key.ViewedByUserName, viewCount = group.LongCount(),
                firstViewedAt = group.Min(item => item.ViewedAt), lastViewedAt = group.Max(item => item.ViewedAt),
                contentLabel = group.OrderByDescending(item => item.ViewedAt).Select(item => item.ContentLabel).FirstOrDefault(),
                sourcePageName = group.OrderByDescending(item => item.ViewedAt).Select(item => item.SourcePageName).FirstOrDefault()
            }).OrderByDescending(item => item.lastViewedAt).ToListAsync(ct);
        return Ok(new { success = true, rows });
    }

    [HttpGet("/Order/GetOrderContentViewUnreadCounts")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetOrderContentViewUnreadCounts([FromQuery] int orderId, CancellationToken ct)
    {
        var readerId = User.GetUserId() ?? string.Empty;
        var states = await _context.OrderContentViewReadStates.AsNoTracking().Where(item => item.OrderId == orderId && item.ReaderUserId == readerId).ToListAsync(ct);
        var stateMap = states.ToDictionary(item => (item.ContentType, item.ContentKey), item => item.LastReadAt);
        var logs = await _context.OrderContentViewLogs.AsNoTracking().Where(item => item.OrderId == orderId).Select(item => new { item.ContentType, item.ContentKey, item.ViewedAt }).ToListAsync(ct);
        var rows = logs.GroupBy(item => new { item.ContentType, item.ContentKey }).Select(group => new
        {
            contentType = group.Key.ContentType, contentKey = group.Key.ContentKey,
            unreadCount = group.LongCount(item => item.ViewedAt > stateMap.GetValueOrDefault((item.ContentType, item.ContentKey), DateTime.MinValue))
        }).Where(item => item.unreadCount > 0).ToList();
        return Ok(new { success = true, rows });
    }

    [HttpPost("/Order/MarkOrderContentViewsRead")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> MarkOrderContentViewsRead([FromForm] int orderId, [FromForm] string contentType, [FromForm] string contentKey, CancellationToken ct)
    {
        var readerId = User.GetUserId() ?? string.Empty;
        contentType = Normalize(contentType, 80, "OrderContent"); contentKey = Normalize(contentKey, 500, "default");
        var last = await _context.OrderContentViewLogs.AsNoTracking().Where(item => item.OrderId == orderId && item.ContentType == contentType && item.ContentKey == contentKey).MaxAsync(item => (DateTime?)item.ViewedAt, ct) ?? IstanbulTimeHelper.Now;
        var state = await _context.OrderContentViewReadStates.FirstOrDefaultAsync(item => item.OrderId == orderId && item.ContentType == contentType && item.ContentKey == contentKey && item.ReaderUserId == readerId, ct);
        if (state is null) _context.OrderContentViewReadStates.Add(new OrderContentViewReadState { OrderId = orderId, ContentType = contentType, ContentKey = contentKey, ReaderUserId = readerId, LastReadAt = last });
        else state.LastReadAt = last;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, unreadCount = 0 });
    }

    [HttpGet("/Order/GetOrderEditHistoryOverview")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetOrderEditHistoryOverview([FromQuery] int orderId, CancellationToken ct)
    {
        var rows = await _context.OrderDetailsFieldAuditLogs.AsNoTracking().Where(item => item.OrderId == orderId && item.ActionType == "Edit")
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(300)
            .Select(item => new { item.Id, item.FieldKey, item.FieldLabel, item.OldValue, item.NewValue, item.ChangeReason, item.SourcePageName, userName = item.CreatedByUserName, item.CreatedAt }).ToListAsync(ct);
        return Ok(new { success = true, rows });
    }

    [HttpGet("/Order/GetPendingOrderPackagingAchievement")]
    [Authorize(Roles = "Admin,Administrator")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetPendingOrderPackagingAchievement(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var row = await (from notification in _context.OrderPackagingAchievementNotifications.AsNoTracking()
                         join run in _context.OrderPackagingAchievementRuns.AsNoTracking() on notification.RunId equals run.Id
                         where notification.UserId == userId && notification.AcknowledgedAt == null
                         orderby notification.Id
                         select new { notification.Id, run.EmployeeName, run.OrderCount, run.WorkStartedAt, run.CompletedAt, run.DurationSeconds, run.OrderIds }).FirstOrDefaultAsync(ct);
        return row is null ? Ok(new { success = true, hasPending = false }) : Ok(new { success = true, hasPending = true, notificationId = row.Id, payload = row });
    }

    [HttpPost("/Order/AcknowledgeOrderPackagingAchievement")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> AcknowledgeOrderPackagingAchievement([FromForm] long id, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var row = await _context.OrderPackagingAchievementNotifications.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId, ct);
        if (row is null) return NotFound(new { success = false });
        row.AcknowledgedAt ??= IstanbulTimeHelper.Now;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private async Task<string> CurrentAuditUserName(CancellationToken ct)
    {
        var userId = User.GetUserId();
        return await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId == userId).Select(employee => employee.DisplayName ?? employee.Name).FirstOrDefaultAsync(ct) ?? User.Identity?.Name ?? "غير محدد";
    }

    private static string Normalize(string? value, int maxLength, string fallback) => Limit(string.IsNullOrWhiteSpace(value) ? fallback : value, maxLength)!;
    private static string? Limit(string? value, int maxLength) { value = value?.Trim(); return value?.Length > maxLength ? value[..maxLength] : value; }
}
