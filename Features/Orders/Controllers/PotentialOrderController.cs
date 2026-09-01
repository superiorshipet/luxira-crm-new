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
    [HttpGet("GetOrders")]
    public async Task<ActionResult<List<PotentialOrder>>> GetPotentialOrders([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.PotentialOrders.AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            query = query.Where(p => p.Country == country.Value);
        }

        var list = await query.Where(p => !p.IsConverted)
            .OrderByDescending(p => p.CreatedDate)
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
            TelephoneNumber = request.TelephoneNumber,
            Address = request.Address,
            Notes = request.Notes,
            Country = request.Country,
            TotalPrice = request.TotalPrice,
            ChatUrl = request.ChatUrl,
            PageName = request.PageName,
            PostUrl = request.PostUrl,
            AssignedUserId = User.GetUserId(),
            CreatedDate = DateTime.UtcNow
        };

        await _context.PotentialOrders.AddAsync(pot, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(pot);
    }

    [HttpPost("{id:int}/convert")]
    [HttpPost("ConvertToOrder/{id:int}")]
    public async Task<ActionResult<OrderDto>> ConvertToOrder([FromRoute] int id, [FromBody] ConvertPotentialOrderRequest request, CancellationToken ct)
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
            SourceName: pot.PageName,
            ManufacturingCompanyId: request.ManufacturingCompanyId,
            DeliveryCompanyId: request.DeliveryCompanyId,
            TelephoneNumber: pot.TelephoneNumber,
            SecondTelephoneNumber: request.SecondTelephoneNumber,
            CustomerName: pot.CustomerName,
            Notes: pot.Notes,
            Address: pot.Address ?? request.Address,
            TotalPrice: pot.TotalPrice ?? request.TotalPrice,
            DeliveryPrice: request.DeliveryPrice,
            CustomerDeliveryPrice: request.CustomerDeliveryPrice,
            ChatUrl: pot.ChatUrl,
            Items: request.Items
        );

        var userId = User.GetUserId() ?? "system";
        var order = await _orderService.CreateOrderAsync(createReq, userId, ct);

        pot.IsConverted = true;
        pot.ConvertedOrderId = order.Id;
        await _context.SaveChangesAsync(ct);

        return Ok(order);
    }
}

public record CreatePotentialOrderRequest(string CustomerName, string TelephoneNumber, string? Address, string? Notes, int Country, decimal? TotalPrice, string? ChatUrl, string? PageName, string? PostUrl);
public record ConvertPotentialOrderRequest(string State, int OrderSource, int? ManufacturingCompanyId, int DeliveryCompanyId, string? SecondTelephoneNumber, string Address, decimal TotalPrice, decimal DeliveryPrice, decimal CustomerDeliveryPrice, List<CreateOrderItemRequest>? Items);
