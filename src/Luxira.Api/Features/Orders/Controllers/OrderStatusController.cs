using System.Security.Claims;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders")]
[Route("api/[controller]")]
public class OrderStatusController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrderStatusController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPut("{orderId:int}/status")]
    [HttpPost("/Order/UpdateStatus/{orderId:int}")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        [FromRoute] int orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var result = await _orderService.UpdateOrderStatusAsync(orderId, request, userId, ct);
        return Ok(result);
    }

    [HttpPost("batch-status")]
    [HttpPost("/Order/BatchUpdateStatus")]
    public async Task<IActionResult> BatchUpdateStatus(
        [FromBody] BatchUpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        int updated = await _orderService.BatchUpdateOrderStatusAsync(request, userId, ct);
        return Ok(new { updatedCount = updated, message = $"Successfully updated {updated} orders." });
    }
}
