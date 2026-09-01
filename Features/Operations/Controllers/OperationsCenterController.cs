using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/operations/center")]
[Route("OperationsCenter")]
public class OperationsCenterController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OperationsCenterController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetLiveStats")]
    public async Task<IActionResult> GetLiveStats([FromQuery] int? country, CancellationToken ct)
    {
        var ordersQuery = _context.Orders.AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            ordersQuery = ordersQuery.Where(o => o.Country == country.Value);
        }

        int totalOrders = await ordersQuery.CountAsync(ct);
        int pendingOrders = await ordersQuery.CountAsync(o => o.OrderStatus == 1, ct);
        int preparedOrders = await ordersQuery.CountAsync(o => o.OrderStatus == 4, ct);
        int inTransitOrders = await ordersQuery.CountAsync(o => o.OrderStatus == 6, ct);
        int deliveredOrders = await ordersQuery.CountAsync(o => o.OrderStatus == 5, ct);
        int returnedOrders = await ordersQuery.CountAsync(o => o.OrderStatus == 7, ct);

        return Ok(new
        {
            totalOrders,
            pendingOrders,
            preparedOrders,
            inTransitOrders,
            deliveredOrders,
            returnedOrders,
            serverTime = DateTime.UtcNow
        });
    }
}
