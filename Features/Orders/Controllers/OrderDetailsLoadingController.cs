using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/loading")]
[Route("OrderDetailsLoading")]
public class OrderDetailsLoadingController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrderDetailsLoadingController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("BatchMeta")]
    public async Task<IActionResult> BatchMeta([FromQuery] string? ids, CancellationToken ct)
    {
        var orderIds = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .Take(50)
            .ToList();

        if (orderIds.Count == 0)
        {
            return Ok(new { success = true, items = Array.Empty<object>() });
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .Select(o => new
            {
                id = o.Id,
                customerName = o.CustomerName,
                telephoneNumber = o.TelephoneNumber,
                address = o.Address,
                orderStatus = o.OrderStatus,
                totalPrice = o.TotalPrice,
                chatUrl = o.Chaturl,
                createdDate = o.CreatedDate
            })
            .ToListAsync(ct);

        return Ok(new { success = true, items = orders });
    }
}
