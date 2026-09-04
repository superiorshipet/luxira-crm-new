using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Luxira.Api.Infrastructure.Webhooks;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/webhooks/camex")]
[Route("api/camex/webhook")]
[Route("CamexWebhook")]
public class CamexWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;
    private readonly WebhookSecurity _webhookSecurity;

    public CamexWebhookController(
        ApplicationDbContext context,
        OrderService orderService,
        WebhookSecurity webhookSecurity)
    {
        _context = context;
        _orderService = orderService;
        _webhookSecurity = webhookSecurity;
    }

    [HttpPost]
    [HttpPost("ProcessWebhook")]
    public async Task<IActionResult> ProcessWebhook([FromBody] CamexWebhookPayload payload, CancellationToken ct)
    {
        _webhookSecurity.ValidateSharedSecret(Request, "Camex");
        if (payload == null || string.IsNullOrWhiteSpace(payload.TrackingNumber))
        {
            return BadRequest(new { message = "Invalid payload" });
        }

        if (!long.TryParse(
                payload.TrackingNumber,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var trackingNumber))
        {
            return BadRequest(new { message = "Tracking number must be numeric." });
        }

        var normalizedStatus = payload.Status?.Trim().ToLowerInvariant();
        var targetStatus = normalizedStatus switch
        {
            "delivered" => OrderStatusCodes.Delivered,
            "returned" => OrderStatusCodes.Returned,
            "in_transit" => OrderStatusCodes.InDelivery,
            "cancelled" => OrderStatusCodes.Cancelled,
            _ => throw new BadRequestException("Unsupported Camex webhook status.")
        };

        var eventKey = payload.EventId ??
            $"{payload.TrackingNumber}|{normalizedStatus}|{payload.Timestamp:O}|{payload.Notes}";
        await _webhookSecurity.ExecuteOnceAsync("Camex", eventKey, async cancellationToken =>
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.CamexTrackingNumber == trackingNumber, cancellationToken)
                ?? throw new NotFoundException("Order for Camex tracking number was not found.");
            if (targetStatus != order.OrderStatus)
            {
                await _orderService.UpdateOrderStatusAsync(
                    order.Id,
                    new Orders.DTOs.UpdateOrderStatusRequest(targetStatus, $"Camex Webhook: {payload.Status}", payload.Notes),
                    OrderStatusActor.TrustedSystem("camex-webhook"),
                    cancellationToken);
            }
        }, ct);

        return Ok(new { success = true, trackingNumber = payload.TrackingNumber });
    }
}

public record CamexWebhookPayload(
    string TrackingNumber,
    string? Status,
    string? Notes,
    DateTime? Timestamp,
    string? EventId = null);
