using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,CallCenter,FollowUpDepartment,ExecutiveDirector")]
[Route("api/v1/orders/pending-verification")]
[Route("PendingVerification")]
public sealed class PendingVerificationController(ApplicationDbContext context, CourierDispatchService dispatch, OrderService orders) : ControllerBase
{
    private static readonly string[] CourierKeys = ["sandoog", "camex"];

    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var companies = new List<object>();
        foreach (var key in CourierKeys)
        {
            var configured = dispatch.IsConfigured(key); var count = configured ? await dispatch.PendingOrders(key).CountAsync(ct) : 0;
            companies.Add(new { key, displayName = key == "sandoog" ? "شركة صندوق" : "شركة كامكس", isConfigured = configured, deliveryCompanyId = dispatch.DeliveryCompanyId(key), pendingCount = count, configurationError = configured ? null : $"إعدادات {key} غير مكتملة." });
        }
        return Ok(companies);
    }

    [HttpGet("Company")]
    public async Task<IActionResult> Company([FromQuery] string id, CancellationToken ct)
    {
        id = id?.Trim().ToLowerInvariant() ?? string.Empty; if (!CourierKeys.Contains(id)) return NotFound();
        var configured = dispatch.IsConfigured(id); if (!configured) return Ok(new { key = id, isConfigured = false, configurationError = $"إعدادات {id} غير مكتملة.", orders = Array.Empty<object>() });
        var rows = await dispatch.PendingOrders(id).AsNoTracking().OrderByDescending(order => order.CreatedDate).Take(500).ToListAsync(ct);
        var items = new List<object>(rows.Count); foreach (var order in rows) items.Add(new { orderId = order.Id, order.CustomerName, order.TelephoneNumber, order.State, order.Address, order.TotalPrice, order.Notes, order.CreatedDate, sendAttempts = id == "sandoog" ? order.SandoogSendAttempts : order.CamexSendAttempts, dataProblem = await dispatch.DescribeDataProblem(id, order, ct) });
        var schedule = await context.ScheduledSendRequests.AsNoTracking().Where(item => item.CourierKey == id && item.Status == Models.ScheduledSendStatus.Pending).OrderBy(item => item.FireAtUtc).FirstOrDefaultAsync(ct);
        return Ok(new { key = id, displayName = id == "sandoog" ? "شركة صندوق" : "شركة كامكس", isConfigured = true, orders = items, pendingSchedule = schedule });
    }

    [HttpGet("queue")]
    [HttpGet("/PendingVerification/GetQueue")]
    public async Task<ActionResult<List<OrderDto>>> GetPendingVerificationOrders([FromQuery] int? deliveryCompanyId, CancellationToken ct)
    {
        var result = await orders.GetOrdersAsync(new OrderFilterRequest(Status: Models.OrderStatusCodes.New, DeliveryCompanyId: deliveryCompanyId, Page: 1, PageSize: 100), ct);
        return Ok(result.Items);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmApi([FromBody] ConfirmShipmentRequest request, CancellationToken ct)
    {
        var courier = CourierKeys.FirstOrDefault(key => dispatch.DeliveryCompanyId(key) == request.DeliveryCompanyId) ?? request.CourierKey?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(courier)) return BadRequest(new { success = false, message = "Courier is required." });
        return CourierResult(await dispatch.ConfirmAsync(courier, request.OrderId, ct));
    }

    [HttpPost("/PendingVerification/Confirm")]
    public async Task<IActionResult> Confirm([FromForm] string id, [FromForm] int orderId, CancellationToken ct) => CourierResult(await dispatch.ConfirmAsync(id, orderId, ct));

    [HttpPost("ConfirmAll")]
    public async Task<IActionResult> ConfirmAll([FromForm] string id, [FromForm] List<int> orderIds, CancellationToken ct)
    {
        id = id?.Trim().ToLowerInvariant() ?? string.Empty; if (!CourierKeys.Contains(id)) return BadRequest(new { success = false, message = "شركة التوصيل غير معروفة." });
        var attempted = orderIds.Distinct().Take(25).ToList(); var remaining = Math.Max(0, orderIds.Distinct().Count() - attempted.Count); var results = new List<object>(); var sent = 0; var blocked = 0; var deferred = 0;
        foreach (var orderId in attempted) { var result = await dispatch.ConfirmAsync(id, orderId, ct); if (result.Outcome == CourierConfirmOutcome.Sent) sent++; else if (result.Outcome == CourierConfirmOutcome.Blocked) blocked++; else deferred++; results.Add(new { orderId, outcome = result.Outcome.ToString().ToLowerInvariant(), message = result.Message }); }
        return Ok(new { success = true, sent, blocked, deferred, notAttempted = remaining, results });
    }

    [HttpPost("Schedule")]
    public async Task<IActionResult> Schedule([FromForm] string id, [FromForm] int delayMinutes, [FromForm] int orderCount, CancellationToken ct)
    {
        id = id?.Trim().ToLowerInvariant() ?? string.Empty; if (!CourierKeys.Contains(id) || !dispatch.IsConfigured(id)) return BadRequest(new { success = false, message = "شركة التوصيل غير معروفة أو غير مهيأة." });
        if (delayMinutes is < 1 or > 1440) return BadRequest(new { success = false, message = "المدة يجب أن تكون بين 1 و 1440 دقيقة." }); if (orderCount is < 1 or > 10) return BadRequest(new { success = false, message = "عدد الطلبات يجب أن يكون بين 1 و 10." });
        if (await context.ScheduledSendRequests.AsNoTracking().AnyAsync(item => item.CourierKey == id && item.Status == Models.ScheduledSendStatus.Pending, ct)) return Conflict(new { success = false, message = "يوجد إرسال مجدول قيد الانتظار لهذه الشركة بالفعل." });
        var now = DateTime.UtcNow; var row = new Models.ScheduledSendRequest { CourierKey = id, RequestedByUserId = User.GetUserId(), RequestedAtUtc = now, DelayMinutes = delayMinutes, OrderCount = orderCount, FireAtUtc = now.AddMinutes(delayMinutes), Status = Models.ScheduledSendStatus.Pending };
        context.ScheduledSendRequests.Add(row); try { await context.SaveChangesAsync(ct); } catch (DbUpdateException) { return Conflict(new { success = false, message = "يوجد إرسال مجدول قيد الانتظار لهذه الشركة بالفعل." }); }
        return Ok(new { success = true, scheduleId = row.Id, fireAtUtc = row.FireAtUtc });
    }

    [HttpPost("CancelSchedule")]
    public async Task<IActionResult> CancelSchedule([FromForm] string id, [FromForm] int scheduleId, CancellationToken ct)
    {
        var affected = await context.ScheduledSendRequests.Where(item => item.Id == scheduleId && item.CourierKey == id && item.Status == Models.ScheduledSendStatus.Pending)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, Models.ScheduledSendStatus.Cancelled).SetProperty(item => item.CancelledByUserId, User.GetUserId()).SetProperty(item => item.CancelledAtUtc, DateTime.UtcNow), ct);
        return affected == 0 ? NotFound(new { success = false, message = "لم يعد هناك إرسال مجدول قيد الانتظار." }) : Ok(new { success = true });
    }

    private OkObjectResult CourierResult(CourierConfirmResult result) => Ok(new { success = result.Outcome == CourierConfirmOutcome.Sent, outcome = result.Outcome.ToString().ToLowerInvariant(), message = result.Message, externalReference = result.ExternalReference });
}

public sealed record ConfirmShipmentRequest(int OrderId, string? CourierKey = null, int? DeliveryCompanyId = null);
