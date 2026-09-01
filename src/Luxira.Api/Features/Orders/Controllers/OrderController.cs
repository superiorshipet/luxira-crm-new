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
public class OrderController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrderController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [HttpGet("/Order/GetOrders")]
    public async Task<ActionResult<OrderListResult>> GetOrders([FromQuery] OrderFilterRequest filter, CancellationToken ct)
    {
        var result = await _orderService.GetOrdersAsync(filter, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HttpGet("/Order/GetOrderById/{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id, CancellationToken ct)
    {
        var result = await _orderService.GetOrderByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HttpPost("/Order/Create")]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var result = await _orderService.CreateOrderAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetOrderById), new { id = result.Id }, result);
    }

    [HttpGet("stats")]
    [HttpGet("/Order/GetStats")]
    public async Task<ActionResult<OrderStatsDto>> GetStats([FromQuery] int? country, CancellationToken ct)
    {
        var stats = await _orderService.GetStatsAsync(country, ct);
        return Ok(stats);
    }
}
