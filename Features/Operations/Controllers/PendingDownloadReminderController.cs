using Luxira.Api.Data;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/operations/pending-downloads")]
[Route("PendingDownloadReminder")]
public class PendingDownloadReminderController(ApplicationDbContext context) : ControllerBase
{
    [HttpPost("Check")]
    [HttpPost("/PendingDownloadReminder/Check")]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        var pendingCount = await context.Orders.AsNoTracking()
            .CountAsync(order => order.OrderStatus == OrderStatusCodes.New && !order.IsHidden, ct);
        return Ok(new
        {
            hasPendingDownloads = pendingCount > 0,
            pendingCount,
            message = pendingCount > 0 ? "There are pending new orders." : "Queue clear."
        });
    }

    [HttpGet("Unread")]
    [HttpGet("/PendingDownloadReminder/Unread")]
    public async Task<IActionResult> Unread(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var notifications = await context.AdminNotifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId && !notification.IsRead)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return Ok(notifications);
    }

    [HttpPost("MarkRead")]
    [HttpPost("/PendingDownloadReminder/MarkRead")]
    public async Task<IActionResult> MarkRead([FromQuery] long id, CancellationToken ct)
    {
        if (id is <= 0 or > int.MaxValue) throw new BadRequestException("Invalid notification ID.");
        var userId = User.GetUserId() ?? string.Empty;
        var notification = await context.AdminNotifications.FirstOrDefaultAsync(
            item => item.Id == (int)id && item.RecipientUserId == userId,
            ct);
        if (notification is null) throw new NotFoundException("Notification was not found.");
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(ct);
        }
        return Ok(new { success = true, id });
    }
}

[ApiController]
[AllowAnonymous]
[Route("api/client-timing")]
public class ClientTimingController : ControllerBase
{
    [HttpPost("home-table")]
    public IActionResult HomeTable([FromBody] object payload)
    {
        // Telemetry / timing ingestion endpoint
        return NoContent();
    }
}
