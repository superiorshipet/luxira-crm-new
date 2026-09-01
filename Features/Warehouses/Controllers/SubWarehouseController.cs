using Luxira.Api.Data;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Warehouses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/warehouses/sub")]
[Route("SubWarehouse")]
public class SubWarehouseController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SubWarehouseController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetSubWarehouses")]
    public async Task<ActionResult<List<SubWarehouse>>> GetSubWarehouses([FromQuery] int? mainWarehouseId, CancellationToken ct)
    {
        var query = _context.SubWarehouses.AsNoTracking().AsQueryable();
        if (mainWarehouseId.HasValue && mainWarehouseId.Value > 0)
        {
            query = query.Where(s => s.MainWarehouseId == mainWarehouseId.Value);
        }

        var list = await query.ToListAsync(ct);
        return Ok(list);
    }
}
