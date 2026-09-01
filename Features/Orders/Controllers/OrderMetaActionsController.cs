using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/{id:int}/meta")]
[Route("Order")]
public class OrderMetaActionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;

    public OrderMetaActionsController(ApplicationDbContext context, OrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    [HttpPost("pin")]
    [HttpPost("/Order/PinOrder/{id:int}")]
    public async Task<IActionResult> TogglePin([FromRoute] int id, CancellationToken ct)
    {
        var order = await _context.Orders.FindAsync([id], ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {id} not found.");
        }

        order.IsPinned = !order.IsPinned;
        order.PinnedAt = order.IsPinned ? DateTime.UtcNow : null;
        order.PinnedByUserId = order.IsPinned ? User.GetUserId() : null;

        await _context.SaveChangesAsync(ct);
        return Ok(new { isPinned = order.IsPinned });
    }

    [HttpPost("delayed")]
    [HttpPost("/Order/SetDelayed/{id:int}")]
    public async Task<IActionResult> ToggleDelayed([FromRoute] int id, [FromBody] SetDelayedRequest? request, CancellationToken ct)
    {
        var order = await _context.Orders.FindAsync([id], ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {id} not found.");
        }

        order.IsDelayed = request?.IsDelayed ?? !order.IsDelayed;
        order.LastEditedDate = DateTime.UtcNow;
        order.Editedby = User.GetUserId();

        await _context.SaveChangesAsync(ct);
        return Ok(new { isDelayed = order.IsDelayed });
    }

    [HttpPost("special-client")]
    [HttpPost("/Order/SetSpecialClient/{id:int}")]
    public async Task<IActionResult> ToggleSpecialClient([FromRoute] int id, CancellationToken ct)
    {
        var order = await _context.Orders.FindAsync([id], ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {id} not found.");
        }

        order.IsClientSpecial = !order.IsClientSpecial;
        await _context.SaveChangesAsync(ct);
        return Ok(new { isClientSpecial = order.IsClientSpecial });
    }
}

public record SetDelayedRequest(bool IsDelayed, string? Reason);
