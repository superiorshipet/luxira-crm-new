using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/pending-verification")]
[Route("PendingVerification")]
public class PendingVerificationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;

    public PendingVerificationController(ApplicationDbContext context, OrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    [HttpGet("queue")]
    [HttpGet("/PendingVerification/GetQueue")]
    public async Task<ActionResult<List<OrderDto>>> GetPendingVerificationOrders([FromQuery] int? deliveryCompanyId, CancellationToken ct)
    {
        // Status 1 (طلب جديد) with external/integrated courier
        var filter = new OrderFilterRequest(
            Country: null,
            Status: 1,
            DeliveryCompanyId: deliveryCompanyId,
            Page: 1,
            PageSize: 100
        );

        var result = await _orderService.GetOrdersAsync(filter, ct);
        return Ok(result.Items);
    }

    [HttpPost("confirm")]
    [HttpPost("/PendingVerification/Confirm")]
    public async Task<IActionResult> ConfirmOrderShipment([FromBody] ConfirmShipmentRequest request, CancellationToken ct)
    {
        var order = await _context.Orders.FindAsync([request.OrderId], ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {request.OrderId} not found.");
        }

        var userId = User.GetUserId() ?? "system";
        // Progress to Status 3 (قيد التجهيز) or 4 (جاهز للتسليم)
        var updated = await _orderService.UpdateOrderStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest(
                OrderStatusCodes.Processed,
                "Verified and Dispatched to Courier Queue",
                request.TrackingNumber),
            userId,
            ct);

        if (!string.IsNullOrWhiteSpace(request.TrackingNumber))
        {
            order.PostTrackNumber = request.TrackingNumber;
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { success = true, order = updated });
    }
}

public record ConfirmShipmentRequest(int OrderId, string? TrackingNumber);
