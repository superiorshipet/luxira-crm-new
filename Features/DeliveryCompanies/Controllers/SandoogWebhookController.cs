using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/webhooks/sandoog")]
[Route("SandoogWebhook")]
public class SandoogWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;

    public SandoogWebhookController(ApplicationDbContext context, OrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    [HttpPost]
    [HttpPost("ProcessWebhook")]
    public async Task<IActionResult> ProcessWebhook([FromBody] SandoogWebhookPayload payload, CancellationToken ct)
    {
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

        var order = await _context.Orders
            .Include(o => o.DeliveryCompany)
            .FirstOrDefaultAsync(o => o.Id == payload.OrderId.Value, ct);

        if (order != null)
        {
            if (!string.IsNullOrWhiteSpace(payload.ReasonCode))
            {
                order.SandoogReasonCode = payload.ReasonCode.Trim();
            }

            int targetStatus = payload.Event?.ToLowerInvariant() switch
            {
                "delivered" => OrderStatusCodes.Delivered,
                "returned" => OrderStatusCodes.Returned,
                "shipped" => OrderStatusCodes.InDelivery,
                _ => order.OrderStatus
            };

            if (targetStatus != order.OrderStatus)
            {
                await _orderService.UpdateOrderStatusAsync(
                    order.Id,
                    new Orders.DTOs.UpdateOrderStatusRequest(targetStatus, $"Sandoog Webhook: {payload.Event}", payload.Reason),
                    OrderStatusActor.TrustedSystem("sandoog-webhook"),
                    ct);
            }
            else if (_context.Entry(order).Property(o => o.SandoogReasonCode).IsModified)
            {
                await _context.SaveChangesAsync(ct);
            }
        }

        return Ok(new { success = true, shipmentId = payload.ShipmentId });
    }
}

public record SandoogWebhookPayload(
    string ShipmentId,
    string? Event,
    string? Reason,
    DateTime? Date,
    int? OrderId = null,
    string? ReasonCode = null);
