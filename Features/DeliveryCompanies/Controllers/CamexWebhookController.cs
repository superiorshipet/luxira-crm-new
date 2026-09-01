using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/webhooks/camex")]
[Route("CamexWebhook")]
public class CamexWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;
    private readonly ILogger<CamexWebhookController> _logger;

    public CamexWebhookController(ApplicationDbContext context, OrderService orderService, ILogger<CamexWebhookController> logger)
    {
        _context = context;
        _orderService = orderService;
        _logger = logger;
    }

    [HttpPost]
    [HttpPost("ProcessWebhook")]
    public async Task<IActionResult> ProcessWebhook([FromBody] CamexWebhookPayload payload, CancellationToken ct)
    {
        if (payload == null || string.IsNullOrWhiteSpace(payload.TrackingNumber))
        {
            return BadRequest(new { message = "Invalid payload" });
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.CamexShipmentId == payload.TrackingNumber || o.PostTrackNumber == payload.TrackingNumber, ct);

        if (order != null)
        {
            // Map Camex status to Luxira status (e.g. Delivered = 5, Returned = 7)
            int targetStatus = payload.Status?.ToLowerInvariant() switch
            {
                "delivered" => 5, // تم التوصيل
                "returned" => 7,  // مرتجع
                "in_transit" => 6, // تم الشحن
                "cancelled" => 9,  // ملغي
                _ => order.OrderStatus
            };

            if (targetStatus != order.OrderStatus)
            {
                await _orderService.UpdateOrderStatusAsync(
                    order.Id,
                    new Orders.DTOs.UpdateOrderStatusRequest(targetStatus, $"Camex Webhook: {payload.Status}", payload.Notes),
                    "camex-webhook",
                    ct);
            }
        }

        return Ok(new { success = true, trackingNumber = payload.TrackingNumber });
    }
}

public record CamexWebhookPayload(string TrackingNumber, string? Status, string? Notes, DateTime? Timestamp);
