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
[Authorize(Roles = "Admin,Administrator")]
[Route("api/v1/orders/potential")]
[Route("PotentialOrder")]
public class PotentialOrderController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;

    public PotentialOrderController(ApplicationDbContext context, OrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpPost("Index")]
    [HttpGet("GetOrders")]
    public async Task<ActionResult<List<PotentialOrder>>> GetPotentialOrders([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.PotentialOrders.AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            query = query.Where(p => p.Country == country.Value);
        }

        var list = await query.OrderByDescending(p => p.CreatedDate)
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<PotentialOrder>> CreatePotentialOrder([FromBody] CreatePotentialOrderRequest request, CancellationToken ct)
    {
        var pot = new PotentialOrder
        {
            CustomerName = request.CustomerName,
            PhoneNumber = request.PhoneNumber,
            Country = request.Country,
            ChatUrl = request.ChatUrl,
            StoreName = request.StoreName,
            Status = request.Status,
            OrderSource = request.OrderSource,
            ApplicationUserId = User.GetUserId() ?? "system",
            CreatedDate = DateTime.UtcNow
        };

        await _context.PotentialOrders.AddAsync(pot, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(pot);
    }

    [HttpPost("{id:int}/convert")]
    [HttpPost("ConvertToOrder/{id:int}")]
    public async Task<ActionResult<OrderDto>> ConvertToOrder([RouteOrRequest] int id, [FromBody] ConvertPotentialOrderRequest request, CancellationToken ct)
    {
        var pot = await _context.PotentialOrders.FindAsync([id], ct);
        if (pot == null)
        {
            throw new NotFoundException($"Potential order {id} not found.");
        }

        var createReq = new CreateOrderRequest(
            Country: pot.Country,
            State: request.State,
            OrderSource: request.OrderSource,
            SourceName: pot.StoreName,
            ManufacturingCompanyId: request.ManufacturingCompanyId,
            DeliveryCompanyId: request.DeliveryCompanyId,
            TelephoneNumber: pot.PhoneNumber ?? string.Empty,
            SecondTelephoneNumber: request.SecondTelephoneNumber,
            CustomerName: pot.CustomerName ?? string.Empty,
            Notes: request.Notes,
            Address: request.Address,
            TotalPrice: request.TotalPrice,
            DeliveryPrice: request.DeliveryPrice,
            CustomerDeliveryPrice: request.CustomerDeliveryPrice,
            ChatUrl: pot.ChatUrl,
            Items: request.Items ?? new()
        );

        var userId = User.GetUserId() ?? "system";
        var order = await _orderService.CreateOrderAsync(createReq, userId, ct);

        pot.LastEditedDate = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(order);
    }

    [HttpGet("FilterOptions")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> FilterOptions(CancellationToken ct)
    {
        var countries = await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.Country)
            .Select(group => new { id = group.Key, name = group.Key.ToString(), count = group.Count() }).OrderByDescending(item => item.count).ToListAsync(ct);
        var statuses = await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.Status)
            .Select(group => new { id = group.Key, name = group.Key.ToString(), count = group.Count() }).OrderBy(item => item.id).ToListAsync(ct);
        var stores = await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.StoreName)
            .Select(group => new { id = group.Key, name = group.Key, count = group.Count() }).OrderByDescending(item => item.count).ToListAsync(ct);
        var orderSources = await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.OrderSource)
            .Select(group => new { id = group.Key, name = group.Key.ToString(), count = group.Count() }).OrderByDescending(item => item.count).ToListAsync(ct);
        var employees = await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.ApplicationUserId)
            .Select(group => new { id = group.Key, name = group.Key, count = group.Count() }).OrderByDescending(item => item.count).ToListAsync(ct);
        return Ok(new { countries, statuses, stores, orderSources, employees });
    }

    [HttpGet("FilterCounts")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> FilterCounts([FromQuery] string dimension, CancellationToken ct)
    {
        if (dimension.Equals("country", StringComparison.OrdinalIgnoreCase))
            return Ok(await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.Country).Select(group => new { id = group.Key.ToString(), count = group.Count() }).ToListAsync(ct));
        if (dimension.Equals("status", StringComparison.OrdinalIgnoreCase))
            return Ok(await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.Status).Select(group => new { id = group.Key.ToString(), count = group.Count() }).ToListAsync(ct));
        if (dimension.Equals("orderSource", StringComparison.OrdinalIgnoreCase))
            return Ok(await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.OrderSource).Select(group => new { id = group.Key.ToString(), count = group.Count() }).ToListAsync(ct));
        if (dimension.Equals("employee", StringComparison.OrdinalIgnoreCase))
            return Ok(await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.ApplicationUserId).Select(group => new { id = group.Key, count = group.Count() }).ToListAsync(ct));
        return Ok(await _context.PotentialOrders.AsNoTracking().GroupBy(item => item.StoreName).Select(group => new { id = group.Key, count = group.Count() }).ToListAsync(ct));
    }

    [HttpPost("UpdateStatusForMultiple")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> UpdateStatusForMultiple([FromBody] List<string>? ids, CancellationToken ct)
    {
        var parsed = (ids ?? []).Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).Distinct().ToArray();
        if (parsed.Length == 0) return Ok(new { success = false, message = "لم يتم تحديد أي طلبات" });
        var updated = await _context.PotentialOrders.Where(item => parsed.Contains(item.Id) && item.Status != 6)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, item => item.Status + 1).SetProperty(item => item.LastEditedDate, DateTime.UtcNow), ct);
        return updated == 0
            ? Ok(new { success = false, message = "لا توجد طلبات قابلة للترقية" })
            : Ok(new { success = true, message = "تم تحديث حالة الطلبات بنجاح" });
    }
}

public sealed record CreatePotentialOrderRequest(
    string CustomerName,
    string? PhoneNumber,
    int Country,
    string? ChatUrl,
    string StoreName,
    int OrderSource,
    int Status = 0);

public sealed record ConvertPotentialOrderRequest(
    string State,
    int OrderSource,
    int? ManufacturingCompanyId,
    int DeliveryCompanyId,
    string? SecondTelephoneNumber,
    string Address,
    string? Notes,
    decimal TotalPrice,
    decimal DeliveryPrice,
    decimal CustomerDeliveryPrice,
    List<CreateOrderItemRequest>? Items);
