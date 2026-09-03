using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communication/notifications")]
[Route("AdminNotifications")]
public sealed class AdminNotificationsController(ApplicationDbContext context, IHubContext<OrderHub> hub) : ControllerBase
{
    private const string FixedIconUrl = "/images/operations-notifications/luxira-admin-notification.png";

    [HttpGet]
    [HttpGet("Index")]
    [Authorize(Roles = "Admin,Administrator")]
    public Task<IActionResult> Index(CancellationToken ct) => GetSentHistory(1, 10, ct);

    [HttpGet("GetNotifications")]
    public async Task<ActionResult<List<AdminNotification>>> GetNotifications(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        return Ok(await context.AdminNotifications.AsNoTracking()
            .Where(item => item.RecipientUserId == userId)
            .OrderByDescending(item => item.CreatedAt).Take(50).ToListAsync(ct));
    }

    [HttpGet("/GetActiveEmployees")]
    [Authorize(Roles = "Admin,Administrator")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetActiveEmployees(CancellationToken ct)
    {
        var currentUserId = User.GetUserId() ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        var employees = await (from employee in context.Employees.AsNoTracking()
            join user in context.Users.AsNoTracking() on employee.ApplicationUserId equals user.Id
            where employee.IsShown && employee.IsActive && !employee.IsDeleted && user.EmailConfirmed &&
                  (!user.LockoutEnd.HasValue || user.LockoutEnd <= now) && user.Id != currentUserId
            orderby employee.DisplayName ?? employee.Name ?? user.Name
            select new
            {
                employeeId = employee.Id,
                userId = user.Id,
                name = employee.DisplayName ?? employee.Name ?? user.Name ?? user.Email ?? "موظف"
            }).ToListAsync(ct);
        return Ok(new { success = true, employees });
    }

    [HttpPost("/Send")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Send(
        [FromForm] List<string>? recipientUserIds,
        [FromForm] string? message,
        [FromForm] bool waitForReply = false,
        CancellationToken ct = default)
    {
        var targetIds = (recipientUserIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var normalizedMessage = Normalize(message);
        if (targetIds.Length == 0 || normalizedMessage.Length == 0)
            return BadRequest(new { success = false, message = "اختر موظفًا واحدًا على الأقل واكتب محتوى الإشعار." });

        var recipients = await (from employee in context.Employees.AsNoTracking()
            join user in context.Users.AsNoTracking() on employee.ApplicationUserId equals user.Id
            where targetIds.Contains(user.Id) && employee.IsShown && employee.IsActive && !employee.IsDeleted &&
                  (!user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow)
            select new
            {
                EmployeeId = employee.Id,
                UserId = user.Id,
                Name = employee.DisplayName ?? employee.Name ?? user.Name ?? user.Email ?? "موظف"
            }).ToListAsync(ct);
        recipients = recipients.GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        if (recipients.Count != targetIds.Length)
            return BadRequest(new { success = false, message = "يوجد موظف مختار لم يعد حسابه مفعّلًا. حدّث القائمة وحاول مرة أخرى." });

        var adminId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(adminId)) return Unauthorized(new { success = false });
        var adminName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(adminName))
            adminName = await context.Users.AsNoTracking().Where(user => user.Id == adminId)
                .Select(user => user.Name ?? user.UserName ?? user.Email).FirstOrDefaultAsync(ct) ?? "Admin";

        var now = DateTimeOffset.UtcNow;
        var notifications = recipients.Select(recipient => new AdminNotification
        {
            RecipientUserId = recipient.UserId,
            RecipientEmployeeId = recipient.EmployeeId,
            RecipientName = recipient.Name,
            Message = normalizedMessage,
            CreatedByAdminUserId = adminId,
            CreatedByAdminName = adminName,
            CreatedAt = now,
            IconUrl = FixedIconUrl
        }).ToList();
        context.AdminNotifications.AddRange(notifications);
        await context.SaveChangesAsync(ct);
        context.AdminNotificationReplyStates.AddRange(notifications.Select(notification => new AdminNotificationReplyState
        {
            AdminNotificationId = notification.Id,
            RequiresReply = waitForReply
        }));
        await context.SaveChangesAsync(ct);
        await BroadcastChangedAsync(recipients.Select(recipient => recipient.UserId), ct);
        return Ok(new { success = true, recipientCount = recipients.Count, recipientNames = recipients.Select(item => item.Name), message = $"تم إرسال الإشعار إلى {recipients.Count} موظف وحفظه." });
    }

    [HttpGet("/GetPending")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { success = false });
        var items = await (from item in context.AdminNotifications.AsNoTracking()
            join state in context.AdminNotificationReplyStates.AsNoTracking()
                on item.Id equals state.AdminNotificationId into stateRows
            from state in stateRows.DefaultIfEmpty()
            where item.RecipientUserId == userId && !item.IsRead
            orderby item.Id
            select new
            {
                item.Id,
                type = "admin-manual-notification",
                alertType = "admin-manual-notification",
                title = "تنبيه من الإدارة",
                item.Message,
                item.CreatedAt,
                item.IconUrl,
                requireConfirm = true,
                RequiresReply = state != null && state.RequiresReply
            }).Take(20).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpPost("/MarkRead")]
    public async Task<IActionResult> MarkRead([FromForm] int id, [FromForm] string? reply, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { success = false });
        var notification = await context.AdminNotifications.FirstOrDefaultAsync(
            item => item.Id == id && item.RecipientUserId == userId, ct);
        if (notification is null || notification.IsRead)
            return NotFound(new { success = false, message = "الإشعار غير موجود أو تمت قراءته بالفعل." });
        var normalizedReply = Normalize(reply);
        var state = await context.AdminNotificationReplyStates.FirstOrDefaultAsync(item => item.AdminNotificationId == id, ct);
        var requiresReply = state?.RequiresReply == true;
        if (requiresReply && normalizedReply.Length == 0)
            return BadRequest(new { success = false, message = "هذا الإشعار ينتظر جوابًا. اكتب الرد أولًا ثم اضغط موافق." });

        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        if (requiresReply && state is not null)
        {
            state.ReplyText = normalizedReply;
            state.RepliedAt = DateTimeOffset.UtcNow;
            state.ReplySeenByAdmin = false;
        }
        await context.SaveChangesAsync(ct);
        await hub.Clients.User(notification.CreatedByAdminUserId)
            .SendAsync("luxiraAdminManualNotificationReplyChanged", new { notificationId = id }, ct);
        return Ok(new { success = true, replied = requiresReply, reply = state?.ReplyText });
    }

    [HttpPost("/MarkReplySeen")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> MarkReplySeen([FromForm] int id, CancellationToken ct)
    {
        var adminId = User.GetUserId() ?? string.Empty;
        var notificationExists = await context.AdminNotifications.AsNoTracking()
            .AnyAsync(item => item.Id == id && item.CreatedByAdminUserId == adminId, ct);
        if (!notificationExists) return NotFound(new { success = false });
        var state = await context.AdminNotificationReplyStates.FirstOrDefaultAsync(item => item.AdminNotificationId == id, ct);
        if (state is not null && !string.IsNullOrWhiteSpace(state.ReplyText) && !state.ReplySeenByAdmin)
        {
            state.ReplySeenByAdmin = true;
            await context.SaveChangesAsync(ct);
        }
        return Ok(new { success = true });
    }

    [HttpGet("/GetSentHistory")]
    [Authorize(Roles = "Admin,Administrator")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetSentHistory(int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        const int fixedPageSize = 10;
        page = Math.Max(1, page);
        var adminId = User.GetUserId() ?? string.Empty;
        var query = context.AdminNotifications.AsNoTracking().Where(item => item.CreatedByAdminUserId == adminId);
        var totalItems = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)fixedPageSize));
        page = Math.Min(page, totalPages);
        var pageRows = query.OrderByDescending(item => item.Id).Skip((page - 1) * fixedPageSize).Take(fixedPageSize);
        var items = await (from item in pageRows
            join state in context.AdminNotificationReplyStates.AsNoTracking()
                on item.Id equals state.AdminNotificationId into stateRows
            from state in stateRows.DefaultIfEmpty()
            select new
            {
                item.Id, item.RecipientName, item.Message, item.CreatedAt, item.IsRead,
                RequiresReply = state != null && state.RequiresReply,
                ReplyText = state == null ? null : state.ReplyText,
                RepliedAt = state == null ? null : state.RepliedAt,
                ReplySeenByAdmin = state != null && state.ReplySeenByAdmin,
                item.IconUrl
            }).ToListAsync(ct);
        return Ok(new { success = true, items, page, pageSize = fixedPageSize, totalItems, totalPages });
    }

    private static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        return normalized.Length > 1000 ? normalized[..1000] : normalized;
    }

    private async Task BroadcastChangedAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var tasks = userIds.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(userId => hub.Clients.User(userId).SendAsync("luxiraAdminManualNotificationChanged", cancellationToken: ct))
            .Append(hub.Clients.All.SendAsync("luxiraAdminManualNotificationChanged", cancellationToken: ct));
        await Task.WhenAll(tasks);
    }
}
