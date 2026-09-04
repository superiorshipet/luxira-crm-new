using Luxira.Api.Data;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
    [Authorize(Roles = "Admin,Administrator")]
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
    [Authorize(Roles = "Admin,Administrator")]
    public IActionResult Create() => Ok(new SubWarehouseRequest(null, string.Empty, null, null));

    [HttpPost("Create")]
    [Authorize(Roles = "Admin,Administrator")]
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
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Edit([FromQuery] int id, CancellationToken ct)
    {
        var item = await _context.SubWarehouses.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("Edit")]
    [Authorize(Roles = "Admin,Administrator")]
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

    [HttpGet("GetMainWarehousesWithCounts")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetMainWarehousesWithCounts(CancellationToken ct)
    {
        var totalProducts = await _context.SubWarehouses.CountAsync(ct);
        var warehouses = await _context.MainWarehouses.AsNoTracking().OrderBy(item => item.Name).Select(item => new
        {
            item.Id, item.Name, item.ImageUrl, count = _context.SubWarehouses.Count(product => product.MainWarehouseId == item.Id)
        }).ToListAsync(ct);
        return Ok(new { totalProducts, warehouses });
    }

    [HttpGet("GetCountriesWithProductCounts")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetCountriesWithProductCounts(int? mainwarehouseId, CancellationToken ct)
    {
        var productQuery = _context.SubWarehouses.AsNoTracking().AsQueryable();
        if (mainwarehouseId.HasValue) productQuery = productQuery.Where(item => item.MainWarehouseId == mainwarehouseId.Value);
        var productIds = productQuery.Select(item => item.Id);
        var countries = await _context.Warehouses.AsNoTracking().Where(item => item.SubWarehouseId.HasValue && productIds.Contains(item.SubWarehouseId.Value))
            .GroupBy(item => item.Countries).Select(group => new { id = group.Key, name = group.Key.ToString(), count = group.Select(item => item.SubWarehouseId).Distinct().Count() }).OrderBy(item => item.name).ToListAsync(ct);
        return Ok(new { totalProducts = await productQuery.CountAsync(ct), countries });
    }

    [HttpGet("GetActiveDeliveryCompaniesByCountry")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetActiveDeliveryCompaniesByCountry(int countryId, int? mainwarehouseId, CancellationToken ct)
    {
        var products = _context.SubWarehouses.AsNoTracking().AsQueryable();
        if (mainwarehouseId.HasValue) products = products.Where(item => item.MainWarehouseId == mainwarehouseId.Value);
        var productIds = products.Select(item => item.Id);
        var companies = await _context.DeliveryCompanies.AsNoTracking().Where(company => company.Country == countryId && company.IsActive && company.IsShown && !company.IsRepresentative)
            .OrderBy(company => company.DisplayName ?? company.Name).Select(company => new
            {
                company.Id, name = company.DisplayName ?? company.Name,
                count = _context.Warehouses.Where(warehouse => warehouse.DeliveryCompanyId == company.Id && warehouse.SubWarehouseId.HasValue && productIds.Contains(warehouse.SubWarehouseId.Value)).Select(warehouse => warehouse.SubWarehouseId).Distinct().Count()
            }).ToListAsync(ct);
        return Ok(new { totalProducts = await products.CountAsync(ct), companies });
    }

    [HttpGet("GetSubWarehouseDropdownCounts")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetSubWarehouseDropdownCounts(string? dimension, int? mainwarehouseId, int? countryId, CancellationToken ct)
    {
        var warehouses = _context.Warehouses.AsNoTracking().Where(item => item.SubWarehouseId.HasValue);
        if (mainwarehouseId.HasValue) warehouses = warehouses.Where(item => item.MainWarehouseId == mainwarehouseId.Value);
        if (countryId.HasValue) warehouses = warehouses.Where(item => item.Countries == countryId.Value);
        if (string.Equals(dimension, "deliverycompany", StringComparison.OrdinalIgnoreCase))
            return Ok(await warehouses.GroupBy(item => item.DeliveryCompanyId).Select(group => new { id = group.Key, count = group.Select(item => item.SubWarehouseId).Distinct().Count() }).ToListAsync(ct));
        return Ok(await warehouses.GroupBy(item => item.Countries).Select(group => new { id = group.Key, count = group.Select(item => item.SubWarehouseId).Distinct().Count() }).ToListAsync(ct));
    }

    [HttpGet("GetCompanyProductCounts")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetCompanyProductCounts(int? deliveryCompanyId, CancellationToken ct)
    {
        var query = _context.Warehouses.AsNoTracking().Where(item => item.SubWarehouseId.HasValue);
        if (deliveryCompanyId.HasValue) query = query.Where(item => item.DeliveryCompanyId == deliveryCompanyId.Value);
        return Ok(await query.GroupBy(item => item.DeliveryCompanyId).Select(group => new { deliveryCompanyId = group.Key, count = group.Select(item => item.SubWarehouseId).Distinct().Count() }).ToListAsync(ct));
    }

    [HttpGet("Invoice")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Invoice(string? ids, int? mainwarehouseId, int? countryId, int? deliveryCompanyId, CancellationToken ct)
    {
        var query = _context.SubWarehouses.AsNoTracking().AsQueryable();
        var selected = (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).ToArray();
        if (selected.Length > 0) query = query.Where(item => selected.Contains(item.Id));
        if (mainwarehouseId.HasValue) query = query.Where(item => item.MainWarehouseId == mainwarehouseId.Value);
        if (countryId.HasValue) query = query.Where(item => _context.Warehouses.Any(warehouse => warehouse.SubWarehouseId == item.Id && warehouse.Countries == countryId.Value));
        if (deliveryCompanyId.HasValue) query = query.Where(item => _context.Warehouses.Any(warehouse => warehouse.SubWarehouseId == item.Id && warehouse.DeliveryCompanyId == deliveryCompanyId.Value));
        var items = await query.OrderBy(item => item.Name).Select(item => new { item.Id, item.Name, item.ProductCode }).ToListAsync(ct);
        var html = new StringBuilder("<!doctype html><html dir='rtl'><meta charset='utf-8'><body><h1>كشف المنتجات</h1><table><tr><th>الكود</th><th>المنتج</th></tr>");
        foreach (var item in items) html.Append($"<tr><td>{System.Net.WebUtility.HtmlEncode(item.ProductCode)}</td><td>{System.Net.WebUtility.HtmlEncode(item.Name)}</td></tr>");
        html.Append("</table></body></html>");
        return Content(html.ToString(), "text/html; charset=utf-8");
    }

    [HttpGet("GetDeleteInfo")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetDeleteInfo(int id, CancellationToken ct)
    {
        var product = await _context.SubWarehouses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (product is null) return NotFound(new { success = false });
        var warehouseIds = _context.Warehouses.Where(item => item.SubWarehouseId == id).Select(item => item.Id);
        var warehouseCount = await warehouseIds.CountAsync(ct);
        var ordersCount = await _context.OrderWarehouses.AsNoTracking().CountAsync(item => warehouseIds.Contains(item.WarehouseId), ct);
        return Ok(new { success = true, product.Id, product.Name, warehouseCount, ordersCount, hasOrders = ordersCount > 0 });
    }

    [HttpPost("Delete")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Delete([FromForm] int id, [FromForm] bool force, CancellationToken ct)
    {
        var product = await _context.SubWarehouses.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (product is null) return NotFound(new { success = false });
        var warehouseIds = _context.Warehouses.Where(item => item.SubWarehouseId == id).Select(item => item.Id);
        var ordersCount = await _context.OrderWarehouses.CountAsync(item => warehouseIds.Contains(item.WarehouseId), ct);
        if (ordersCount > 0 && !force) return Conflict(new { success = false, requiresConfirmation = true, ordersCount });
        await _context.Warehouses.Where(item => item.SubWarehouseId == id).ExecuteUpdateAsync(setters => setters.SetProperty(item => item.SubWarehouseId, (int?)null), ct);
        _context.SubWarehouses.Remove(product);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id, preservedOrders = ordersCount });
    }
}

public sealed record SubWarehouseRequest(int? Id, string Name, string? ProductCode, int? MainWarehouseId);
