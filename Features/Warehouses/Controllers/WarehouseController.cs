using Luxira.Api.Features.Warehouses.DTOs;
using Luxira.Api.Features.Warehouses.Services;
using Luxira.Api.Data;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Infrastructure.Pdf;
using Luxira.Api.Utils.Binding;
using Luxira.Api.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Warehouses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/warehouses")]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly WarehouseService _service;
    private readonly ApplicationDbContext _context;
    private readonly LuxiraPdfService _pdf;

    public WarehouseController(WarehouseService service, ApplicationDbContext context, LuxiraPdfService pdf)
    {
        _service = service;
        _context = context;
        _pdf = pdf;
    }

    [HttpGet]
    [HttpGet("/Warehouse/GetWarehouses")]
    [Authorize(Roles = "Admin,Administrator,DeliveryCompany,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment")]
    public async Task<ActionResult<List<WarehouseDto>>> GetWarehouses([FromQuery] int? countryId, [FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _service.GetWarehousesAsync(countryId, isActive, ct);
        return Ok(result);
    }

    [HttpGet("/Warehouse/Index")]
    [HttpPost("/Warehouse/Index")]
    [Authorize(Roles = "Admin,DeliveryCompany,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> Index(
        [RouteOrRequest] int? page,
        [RouteOrRequest] int? pageSize,
        [RouteOrRequest] int? countryId,
        [RouteOrRequest] int? deliveryCompanyId,
        [RouteOrRequest] int? mainwarehouseId,
        [RouteOrRequest] int? storeId,
        CancellationToken ct = default)
    {
        var currentPage = Math.Max(page ?? 1, 1);
        var effectivePageSize = Math.Clamp(pageSize ?? 10, 1, 200);
        var query =
            from warehouse in _context.Warehouses.AsNoTracking()
            join deliveryCompany in _context.DeliveryCompanies.AsNoTracking()
                on warehouse.DeliveryCompanyId equals deliveryCompany.Id
            join mainWarehouse in _context.MainWarehouses.AsNoTracking()
                on warehouse.MainWarehouseId equals mainWarehouse.Id into mainWarehouses
            from mainWarehouse in mainWarehouses.DefaultIfEmpty()
            join company in _context.ManufacturingCompanies.AsNoTracking()
                on warehouse.ManufacturingCompanyId equals company.Id into companies
            from company in companies.DefaultIfEmpty()
            where !deliveryCompany.IsRepresentative
            select new { warehouse, deliveryCompany, mainWarehouse, company };

        if (User.IsInRole("DeliveryCompany"))
        {
            var userId = User.GetUserId();
            query = query.Where(row => row.deliveryCompany.UserId == userId);
        }
        if (countryId.HasValue) query = query.Where(row => row.warehouse.Countries == countryId.Value);
        if (deliveryCompanyId.HasValue) query = query.Where(row => row.warehouse.DeliveryCompanyId == deliveryCompanyId.Value);
        if (mainwarehouseId.HasValue) query = query.Where(row => row.warehouse.MainWarehouseId == mainwarehouseId.Value);
        if (storeId.HasValue) query = query.Where(row => row.warehouse.ManufacturingCompanyId == storeId.Value);

        var totalItems = await query.CountAsync(ct);
        var items = await query.OrderByDescending(row => row.warehouse.Id)
            .Skip((currentPage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(row => new
            {
                row.warehouse.Id,
                name = row.warehouse.Name ?? "Unknown",
                productImage = row.mainWarehouse != null ? row.mainWarehouse.ImageUrl ?? "static/DefaultImage.svg" : "static/DefaultImage.svg",
                row.warehouse.Amount,
                row.warehouse.ReservedAmount,
                row.warehouse.Price,
                deliveryCompanyName = row.deliveryCompany.Name,
                manufacturingCompanyName = row.company != null ? row.company.Name : "Unknown Manufacturer",
                row.warehouse.DateAdded,
                row.warehouse.DateUpdated,
                countries = row.warehouse.Countries,
                row.warehouse.IsShown
            })
            .ToListAsync(ct);

        return Ok(new { items, currentPage, pageSize = effectivePageSize, totalItems });
    }

    [HttpGet("{id:int}")]
    [HttpGet("/Warehouse/GetWarehouseById/{id:int}")]
    public async Task<ActionResult<WarehouseDto>> GetWarehouseById(int id, CancellationToken ct)
    {
        var result = await _service.GetWarehouseByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HttpPost("/Warehouse/Create")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<ActionResult<WarehouseDto>> CreateWarehouse([FromBody] CreateWarehouseRequest request, CancellationToken ct)
    {
        var result = await _service.CreateWarehouseAsync(request, ct);
        return CreatedAtAction(nameof(GetWarehouseById), new { id = result.Id }, result);
    }

    [HttpGet("/Warehouse/Create")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> Create(CancellationToken ct) => Ok(new
    {
        subWarehouses = await _context.SubWarehouses.AsNoTracking().OrderBy(item => item.Name).ToListAsync(ct),
        deliveryCompanies = await _context.DeliveryCompanies.AsNoTracking().Where(item => item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct),
        manufacturingCompanies = await _context.ManufacturingCompanies.AsNoTracking().Where(item => item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct)
    });

    [HttpGet("/Warehouse/Edit")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> Edit([FromQuery] int id, CancellationToken ct)
    {
        var item = await WarehouseDetailsQuery().FirstOrDefaultAsync(row => row.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("/Warehouse/Edit")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> Edit([FromQuery] int id, [FromBody] LegacyWarehouseRequest request, CancellationToken ct)
    {
        if (request.Id != 0 && request.Id != id) return NotFound();
        var item = await _context.Warehouses.Include(row => row.SubWarehouse).FirstOrDefaultAsync(row => row.Id == id, ct);
        if (item is null) return NotFound();
        var added = request.Amount - item.Amount;
        if (added > 0) item.UnchangingAmount += added;
        item.Price = request.Price;
        item.Amount = request.Amount;
        item.ManufacturingCompanyId = request.ManufacturingCompanyId;
        item.MainWarehouseId = request.MainWarehouseId;
        item.SubWarehouseId = request.SubWarehouseId;
        if (request.SubWarehouseId.HasValue)
            item.Name = await _context.SubWarehouses.Where(row => row.Id == request.SubWarehouseId).Select(row => row.Name).FirstOrDefaultAsync(ct) ?? item.Name;
        item.DateUpdated = DateTime.UtcNow;
        if (added != 0)
            _context.WarehouseEditHistories.Add(new WarehouseEditHistory { WarehouseId = id, AddedAmount = added, EditDate = DateTime.UtcNow, ApplicationUserId = User.GetUserId() ?? "system" });
        await _context.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpGet("/Warehouse/Details")]
    [HttpPost("/Warehouse/Details")]
    [Authorize(Roles = "Admin,Administrator,DeliveryCompany,ExecutiveDirector,DeliveryRepresentative,FollowUpDepartment")]
    public async Task<IActionResult> Details([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var item = await WarehouseDetailsQuery().FirstOrDefaultAsync(row => row.Id == id, ct);
        if (item is null) return NotFound();
        var statusTotals = await (
            from orderItem in _context.OrderWarehouses.AsNoTracking()
            join order in _context.Orders.AsNoTracking() on orderItem.OrderId equals order.Id
            where orderItem.WarehouseId == id
            group orderItem by order.OrderStatus into status
            select new { Status = status.Key, Amount = status.Sum(row => row.Amount) }).ToListAsync(ct);
        int[] delivered = [6, 13, 14];
        int[] failed = [7, 8, 9, 10, 15];
        return Ok(new { item, totalDeliveredItemsFromSpecificOrders = statusTotals.Where(row => delivered.Contains(row.Status)).Sum(row => row.Amount), totalFailedDeliveredItemsFromSpecificOrders = statusTotals.Where(row => failed.Contains(row.Status)).Sum(row => row.Amount) });
    }

    [HttpPost("/Warehouse/SetIsShown")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> SetIsShown([FromForm] int WareHouseId, [FromForm] bool isShown, CancellationToken ct)
    {
        var changed = await _context.Warehouses.Where(item => item.Id == WareHouseId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsShown, isShown), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [HttpGet("/Warehouse/GetSubWarehouses")]
    [HttpPost("/Warehouse/GetSubWarehouses")]
    public async Task<IActionResult> GetSubWarehouses([FromQuery] int? mainWarehouseId, CancellationToken ct)
    {
        var query = _context.SubWarehouses.AsNoTracking();
        if (mainWarehouseId.HasValue) query = query.Where(item => item.MainWarehouseId == mainWarehouseId);
        return Ok(await query.OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct));
    }

    [HttpPost("/Warehouse/AllWarehousesPdf")]
    public async Task<IActionResult> AllWarehousesPdf([FromForm] int deliveryCompanyId, CancellationToken ct)
    {
        var company = await _context.DeliveryCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == deliveryCompanyId, ct);
        if (company is null) return NotFound();
        var warehouses = await _context.Warehouses.AsNoTracking().Where(item => item.DeliveryCompanyId == deliveryCompanyId).OrderBy(item => item.Name).ToListAsync(ct);
        if (warehouses.Count == 0) return NotFound();
        return File(_pdf.GenerateWarehouseInventoryPdf(company.Name, warehouses), "application/pdf", $"warehouses-{deliveryCompanyId}.pdf");
    }

    [HttpGet("/Warehouse/IndexRepresentative")]
    [HttpPost("/Warehouse/IndexRepresentative")]
    [Authorize(Roles = "Admin,DeliveryRepresentative,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> IndexRepresentative(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? countryId = null,
        [FromQuery] string? cityId = null,
        [FromQuery] int? deliveryRepresentativeId = null,
        [FromQuery] int? mainWarehouseId = null,
        [FromQuery] int? storeId = null,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query =
            from warehouse in _context.Warehouses.AsNoTracking()
            join deliveryCompany in _context.DeliveryCompanies.AsNoTracking()
                on warehouse.DeliveryCompanyId equals deliveryCompany.Id
            join mainWarehouse in _context.MainWarehouses.AsNoTracking()
                on warehouse.MainWarehouseId equals mainWarehouse.Id into mainWarehouses
            from mainWarehouse in mainWarehouses.DefaultIfEmpty()
            join store in _context.ManufacturingCompanies.AsNoTracking()
                on warehouse.ManufacturingCompanyId equals store.Id into stores
            from store in stores.DefaultIfEmpty()
            where deliveryCompany.IsRepresentative
            select new { warehouse, deliveryCompany, mainWarehouse, store };

        if (User.IsInRole("DeliveryRepresentative"))
        {
            var userId = User.GetUserId();
            query = query.Where(row => row.deliveryCompany.UserId == userId);
        }

        if (countryId.HasValue) query = query.Where(row => row.warehouse.Countries == countryId.Value);
        if (!string.IsNullOrWhiteSpace(cityId)) query = query.Where(row => row.warehouse.City == cityId);
        if (deliveryRepresentativeId is > 0) query = query.Where(row => row.deliveryCompany.Id == deliveryRepresentativeId.Value);
        if (mainWarehouseId is > 0) query = query.Where(row => row.warehouse.MainWarehouseId == mainWarehouseId.Value);
        if (storeId is > 0) query = query.Where(row => row.warehouse.ManufacturingCompanyId == storeId.Value);

        var totalItems = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(row => row.warehouse.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.warehouse.Id,
                row.warehouse.Name,
                ProductImage = row.mainWarehouse == null ? null : row.mainWarehouse.ImageUrl,
                row.warehouse.Amount,
                row.warehouse.ReservedAmount,
                row.warehouse.Price,
                DeliveryCompanyName = row.deliveryCompany.Name,
                ManufacturingCompanyName = row.store == null ? null : row.store.Name,
                row.warehouse.DateAdded,
                row.warehouse.DateUpdated,
                row.warehouse.Countries,
                row.warehouse.IsShown,
                row.warehouse.City
            })
            .ToListAsync(ct);

        return Ok(new { items, currentPage = page, pageSize, totalItems });
    }

    private IQueryable<Warehouse> WarehouseDetailsQuery() => _context.Warehouses.AsNoTracking().Include(item => item.MainWarehouse).Include(item => item.SubWarehouse);

}

public sealed record LegacyWarehouseRequest(int Id, decimal Price, int Amount, int? ManufacturingCompanyId, int? MainWarehouseId, int? SubWarehouseId);
