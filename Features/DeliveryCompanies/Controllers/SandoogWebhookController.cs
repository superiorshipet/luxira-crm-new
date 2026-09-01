using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Luxira.Api.Infrastructure.Webhooks;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/webhooks/sandoog")]
[Route("SandoogWebhook")]
public class SandoogWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;
    private readonly WebhookSecurity _webhookSecurity;

    public SandoogWebhookController(
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
    public async Task<IActionResult> ProcessWebhook([FromBody] SandoogWebhookPayload payload, CancellationToken ct)
    {
        _webhookSecurity.ValidateSharedSecret(Request, "Sandoog");
        if (payload == null || string.IsNullOrWhiteSpace(payload.ShipmentId))
        {
            return BadRequest(new { message = "Invalid payload" });
        }

        if (!payload.OrderId.HasValue || payload.OrderId.Value <= 0)
        {
            return BadRequest(new
            {
                message = "orderId is required because the legacy database has no persisted Sandoog shipment-id mapping."
            });
        }

        var normalizedEvent = payload.Event?.Trim().ToLowerInvariant();
        var targetStatus = normalizedEvent switch
        {
            "delivered" => OrderStatusCodes.Delivered,
            "returned" => OrderStatusCodes.Returned,
            "shipped" => OrderStatusCodes.InDelivery,
            _ => throw new BadRequestException("Unsupported Sandoog webhook event.")
        };
        var eventKey = payload.EventId ??
            $"{payload.ShipmentId}|{normalizedEvent}|{payload.Date:O}|{payload.ReasonCode}";

        await _webhookSecurity.ExecuteOnceAsync("Sandoog", eventKey, async cancellationToken =>
        {
            var order = await _context.Orders
                .Include(o => o.DeliveryCompany)
                .FirstOrDefaultAsync(o => o.Id == payload.OrderId.Value, cancellationToken)
                ?? throw new NotFoundException("Order for Sandoog shipment was not found.");
            if (!string.IsNullOrWhiteSpace(payload.ReasonCode))
            {
                order.SandoogReasonCode = payload.ReasonCode.Trim();
            }

            if (targetStatus != order.OrderStatus)
            {
                await _orderService.UpdateOrderStatusAsync(
                    order.Id,
                    new Orders.DTOs.UpdateOrderStatusRequest(targetStatus, $"Sandoog Webhook: {payload.Event}", payload.Reason),
                    OrderStatusActor.TrustedSystem("sandoog-webhook"),
                    cancellationToken);
            }
            else if (_context.Entry(order).Property(o => o.SandoogReasonCode).IsModified)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }, ct);

        return Ok(new { success = true, shipmentId = payload.ShipmentId });
    }
}

public record SandoogWebhookPayload(
    string ShipmentId,
    string? Event,
    string? Reason,
    DateTime? Date,
    int? OrderId = null,
    string? ReasonCode = null,
    string? EventId = null);
