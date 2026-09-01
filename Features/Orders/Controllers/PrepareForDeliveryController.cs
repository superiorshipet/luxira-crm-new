using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/prepare-for-delivery")]
[Route("PrepareForDelivery")]
public class PrepareForDeliveryController : ControllerBase
{
    private readonly OrderService _orderService;

    public PrepareForDeliveryController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [HttpGet("GetOrders")]
    public async Task<ActionResult<OrderListResult>> GetPreparationOrders([FromQuery] OrderFilterRequest filter, CancellationToken ct)
    {
        // Filter for orders ready for delivery preparation (Status 3 = قيد التجهيز)
        var pfdFilter = filter with { Status = 3 };
        var result = await _orderService.GetOrdersAsync(pfdFilter, ct);
        return Ok(result);
    }

    [HttpPost("scan")]
    [HttpPost("ScanBarcode")]
    public async Task<ActionResult<OrderDto>> ScanBarcode([FromBody] ScanBarcodeRequest request, CancellationToken ct)
    {
        // Mark as Ready for Delivery (Status 4 = جاهز للتسليم)
        var result = await _orderService.UpdateOrderStatusAsync(
            request.OrderId,
            new UpdateOrderStatusRequest(
                OrderStatusCodes.InDelivery,
                "Scanned and Prepared for Delivery",
                request.Barcode),
            OrderStatusActor.FromPrincipal(User),
            ct);

        return Ok(result);
    }
}

public record ScanBarcodeRequest(int OrderId, string Barcode);
