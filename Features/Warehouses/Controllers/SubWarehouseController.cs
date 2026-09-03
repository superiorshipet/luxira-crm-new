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

    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int? pageSize = null, [FromQuery] int? mainwarehouseId = null, CancellationToken ct = default)
    {
        var size = Math.Clamp(pageSize ?? 10, 1, 200);
        var query = _context.SubWarehouses.AsNoTracking();
        if (mainwarehouseId.HasValue) query = query.Where(item => item.MainWarehouseId == mainwarehouseId);
        var totalItems = await query.CountAsync(ct);
        var items = await query.OrderByDescending(item => item.Id).Skip((Math.Max(page, 1) - 1) * size).Take(size).ToListAsync(ct);
        return Ok(new { items, currentPage = Math.Max(page, 1), pageSize = size, totalItems });
    }

    [HttpGet("Create")]
    public IActionResult Create() => Ok(new SubWarehouseRequest(null, string.Empty, null, null));

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] SubWarehouseRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "حقل الاسم مطلوب." });
        var item = new SubWarehouse { Name = request.Name.Trim(), MainWarehouseId = request.MainWarehouseId, ProductCode = request.ProductCode?.Trim() };
        _context.SubWarehouses.Add(item);
        var companies = await _context.DeliveryCompanies.AsNoTracking().ToListAsync(ct);
        foreach (var company in companies)
            _context.Warehouses.Add(new Warehouse
            {
                Name = item.Name,
                SubWarehouse = item,
                DeliveryCompanyId = company.Id,
                MainWarehouseId = request.MainWarehouseId,
                Countries = company.Country,
                City = company.City,
                DateAdded = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                IsShown = true
            });
        await _context.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpGet("Edit")]
    public async Task<IActionResult> Edit([FromQuery] int id, CancellationToken ct)
    {
        var item = await _context.SubWarehouses.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("Edit")]
    public async Task<IActionResult> Edit([FromQuery] int id, [FromBody] SubWarehouseRequest request, CancellationToken ct)
    {
        if (request.Id.HasValue && request.Id != id) return NotFound();
        var item = await _context.SubWarehouses.FirstOrDefaultAsync(row => row.Id == id, ct);
        if (item is null) return NotFound();
        item.Name = request.Name.Trim();
        item.ProductCode = request.ProductCode?.Trim();
        item.MainWarehouseId = request.MainWarehouseId;
        await _context.Warehouses.Where(row => row.SubWarehouseId == id).ExecuteUpdateAsync(update => update.SetProperty(row => row.Name, item.Name).SetProperty(row => row.MainWarehouseId, item.MainWarehouseId), ct);
        await _context.SaveChangesAsync(ct);
        return Ok(item);
    }
}

public sealed record SubWarehouseRequest(int? Id, string Name, string? ProductCode, int? MainWarehouseId);
