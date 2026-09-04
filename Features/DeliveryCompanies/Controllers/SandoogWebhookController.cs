using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController, AllowAnonymous]
[Route("api/v1/webhooks/sandoog"), Route("api/sandoog/webhook"), Route("SandoogWebhook")]
public sealed class SandoogWebhookController(ApplicationDbContext context, OrderService orders, CourierDispatchService courier, IConfiguration configuration, ILogger<SandoogWebhookController> logger) : ControllerBase
{
    private static readonly Dictionary<string, int> Statuses = new(StringComparer.OrdinalIgnoreCase) { ["Requested"] = OrderStatusCodes.New, ["Verified"] = OrderStatusCodes.New, ["ForPacking"] = OrderStatusCodes.Prepared, ["Packed"] = OrderStatusCodes.Prepared, ["PickedUp"] = OrderStatusCodes.InDelivery, ["OnDelivery"] = OrderStatusCodes.InDelivery, ["Delivered"] = OrderStatusCodes.Delivered, ["Complete"] = OrderStatusCodes.Delivered, ["Cancelled"] = OrderStatusCodes.FailedDelivery, ["Returned"] = OrderStatusCodes.FailedDelivery, ["SemiFailed"] = OrderStatusCodes.FailedDelivery, ["Failed"] = OrderStatusCodes.FailedDelivery };
    [HttpPost, HttpPost("ProcessWebhook")]
    public async Task<IActionResult> ProcessWebhook([FromBody] SandoogWebhookPayload payload, CancellationToken ct)
    {
        var callbackKey = configuration["Sandoog:CallbackKey"];
        if (string.IsNullOrWhiteSpace(callbackKey) || !FixedTimeEquals(Request.Headers["Secret-Key"].ToString(), callbackKey)) return Unauthorized();
        var eventType = payload.EventType ?? payload.Event;
        if (string.IsNullOrWhiteSpace(eventType)) return Ok(new { received = true, applied = false, reason = "missing event_type" });
        var reference = payload.ExternalReference ?? payload.OrderId?.ToString(CultureInfo.InvariantCulture);
        if (!int.TryParse(reference, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderId)) return Ok(new { received = true, applied = false, reason = "unrecognised external_reference" });
        var order = await context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return Ok(new { received = true, applied = false, reason = "order not found" });
        var providerId = payload.EventData?.Id ?? payload.ShipmentId;
        if (string.IsNullOrWhiteSpace(order.SandoogOrderId))
        {
            if (order.SandoogConfirmedAt is null || order.SandoogLegacyManual || string.IsNullOrWhiteSpace(providerId)) return Ok(new { received = true, applied = false, reason = "order is not Sandoog-linked" });
            await context.Orders.Where(item => item.Id == order.Id && item.SandoogOrderId == null).ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogOrderId, providerId), ct); order.SandoogOrderId = providerId;
        }
        if (!string.IsNullOrWhiteSpace(providerId) && !string.Equals(providerId, order.SandoogOrderId, StringComparison.Ordinal)) return Ok(new { received = true, applied = false, reason = "sandoog order id mismatch" });
        if (!TryMapStatus(eventType, out var target)) { logger.LogWarning("Unmapped Sandoog event {EventType} for order {OrderId}", eventType, orderId); return Ok(new { received = true, applied = false, reason = "unmapped event_type" }); }
        var fulfillmentId = payload.EventData?.FulfillmentId; var reason = payload.Reason; var reasonCode = payload.ReasonCode; string? label = null;
        if (target == OrderStatusCodes.FailedDelivery && !string.IsNullOrWhiteSpace(order.SandoogOrderId)) { var details = await courier.GetSandoogOrderDetailsAsync(order.SandoogOrderId, eventType, ct); if (details.Success) { fulfillmentId ??= details.FulfillmentId; reason ??= details.Reason; reasonCode ??= details.ReasonCode; label = details.DeliveryLabelUrl; } else logger.LogWarning("Could not enrich Sandoog failure event for order {OrderId}: {Error}", order.Id, details.Error); }
        await context.Orders.Where(item => item.Id == order.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.SandoogFulfillmentId, item => fulfillmentId ?? item.SandoogFulfillmentId).SetProperty(item => item.SandoogReason, item => reason ?? item.SandoogReason).SetProperty(item => item.SandoogReasonCode, item => reasonCode ?? item.SandoogReasonCode).SetProperty(item => item.SandoogDeliveryLabelUrl, item => label ?? item.SandoogDeliveryLabelUrl), ct);
        var changed = target != order.OrderStatus;
        if (changed) await orders.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest(target, $"Sandoog event {eventType}", reason), OrderStatusActor.TrustedSystem("sandoog-webhook"), ct);
        return Ok(new { received = true, applied = changed });
    }
    public static bool TryMapStatus(string? eventType, out int status) { status = default; return !string.IsNullOrWhiteSpace(eventType) && Statuses.TryGetValue(eventType.Trim(), out status); }
    private static bool FixedTimeEquals(string? supplied, string expected) { var left = Encoding.UTF8.GetBytes(supplied ?? string.Empty); var right = Encoding.UTF8.GetBytes(expected); return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right); }
}
public sealed class SandoogWebhookPayload { [JsonPropertyName("event_id")] public string? EventId { get; set; } [JsonPropertyName("event_type")] public string? EventType { get; set; } [JsonPropertyName("created_on")] public string? CreatedOn { get; set; } [JsonPropertyName("external_reference")] public string? ExternalReference { get; set; } [JsonPropertyName("event_data")] public SandoogWebhookEventData? EventData { get; set; } public string? ShipmentId { get; set; } public string? Event { get; set; } public string? Reason { get; set; } public DateTime? Date { get; set; } public int? OrderId { get; set; } public string? ReasonCode { get; set; } }
public sealed class SandoogWebhookEventData { [JsonPropertyName("id")] public string? Id { get; set; } [JsonPropertyName("fulfillment_id")] public string? FulfillmentId { get; set; } }
