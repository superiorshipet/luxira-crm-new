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

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.SandoogShipmentId == payload.ShipmentId || o.PostTrackNumber == payload.ShipmentId, ct);

        if (order != null)
        {
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
                    "sandoog-webhook",
                    ct);
            }
        }

        return Ok(new { success = true, shipmentId = payload.ShipmentId });
    }
}

public record SandoogWebhookPayload(string ShipmentId, string? Event, string? Reason, DateTime? Date);
