using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
[Route("api/v1/manufacturing/country-minimum-prices")]
[Route("CountryMinimumPrices")]
public class CountryMinimumPricesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CountryMinimumPricesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/CountryMinimumPrices/Index")]
    [HttpPost("/CountryMinimumPrices/Index")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var prices = await _context.CountryMinimumPrices
            .AsNoTracking()
            .Select(price => new
            {
                price.Id,
                price.Country,
                price.ManufacturingCompanyId,
                ManufacturingCompanyName = price.ManufacturingCompany != null
                    ? price.ManufacturingCompany.Name
                    : null,
                price.MinimumPriceForOffers,
                price.MaximumPriceForOffers,
            })
            .ToListAsync(ct);
        return Ok(prices);
    }

    [HttpGet("/CountryMinimumPrices/Create")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new
    {
        stores = await _context.ManufacturingCompanies.AsNoTracking().Where(item => item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct)
    });

    [HttpPost("Create")]
    [HttpPost("/CountryMinimumPrices/Create")]
    public async Task<IActionResult> Create([FromBody] CountryMinimumPriceRequest request, CancellationToken ct)
    {
        var minPrice = new CountryMinimumPrice
        {
            Country = request.Country,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            MinimumPriceForOffers = request.MinimumPriceForOffers,
            MaximumPriceForOffers = request.MaximumPriceForOffers,
        };

        await _context.CountryMinimumPrices.AddAsync(minPrice, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(minPrice);
    }

    [HttpGet("Edit/{id:int}")]
    [HttpGet("/CountryMinimumPrices/Edit")]
    public async Task<IActionResult> Edit(
        [RouteOrRequest] int? id,
        [FromQuery(Name = "id")] int? queryId,
        CancellationToken ct)
    {
        var targetId = id ?? queryId;
        if (!targetId.HasValue) return NotFound();

        var price = await _context.CountryMinimumPrices
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == targetId.Value, ct);
        return price is null ? NotFound() : Ok(price);
    }

    [HttpPost("Edit/{id:int}")]
    [HttpPost("/CountryMinimumPrices/Edit")]
    public async Task<IActionResult> Edit(
        [RouteOrRequest] int? id,
        [FromQuery(Name = "id")] int? queryId,
        [FromBody] CountryMinimumPriceRequest request,
        CancellationToken ct)
    {
        var targetId = id ?? queryId ?? request.Id;
        var price = await _context.CountryMinimumPrices
            .FirstOrDefaultAsync(item => item.Id == targetId, ct);
        if (price is null) return NotFound();

        price.Country = request.Country;
        price.ManufacturingCompanyId = request.ManufacturingCompanyId;
        price.MinimumPriceForOffers = request.MinimumPriceForOffers;
        price.MaximumPriceForOffers = request.MaximumPriceForOffers;
        await _context.SaveChangesAsync(ct);
        return Ok(price);
    }
}

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
[Route("api/v1/manufacturing/product-minimum-selling-prices")]
[Route("ProductMinimumSellingPrices")]
public class ProductMinimumSellingPricesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductMinimumSellingPricesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/ProductMinimumSellingPrices/Index")]
    [HttpPost("/ProductMinimumSellingPrices/Index")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var prices = await _context.ProductMinimumSellingPrices.AsNoTracking().ToListAsync(ct);
        return Ok(prices);
    }

    [HttpGet("/ProductMinimumSellingPrices/Create")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new
    {
        products = await _context.MainWarehouses.AsNoTracking().OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct),
        stores = await _context.ManufacturingCompanies.AsNoTracking().Where(item => item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct)
    });

    [HttpGet("/ProductMinimumSellingPrices/Edit")]
    public async Task<IActionResult> EditForm([FromQuery] int id, CancellationToken ct)
    {
        var item = await _context.ProductMinimumSellingPrices.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("Create")]
    [HttpPost("/ProductMinimumSellingPrices/Create")]
    public async Task<IActionResult> Create([FromBody] CreateMinimumPriceRequest request, CancellationToken ct)
    {
        var minPrice = new ProductMinimumSellingPrice
        {
            Country = request.Country,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            MainWarehouseId = request.MainWarehouseId,
            MinimumSellingPrice = request.MinimumSellingPrice,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ProductMinimumSellingPrices.AddAsync(minPrice, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(minPrice);
    }

    [HttpPost("Edit/{id:int}")]
    [HttpPut("{id:int}")]
    [HttpPost("/ProductMinimumSellingPrices/Edit")]
    public async Task<IActionResult> Edit([RouteOrRequest] int id, [FromBody] CreateMinimumPriceRequest request, CancellationToken ct)
    {
        var minPrice = await _context.ProductMinimumSellingPrices.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (minPrice == null) return NotFound("Minimum price configuration not found.");

        minPrice.MinimumSellingPrice = request.MinimumSellingPrice;
        minPrice.Country = request.Country;
        minPrice.ManufacturingCompanyId = request.ManufacturingCompanyId;
        minPrice.MainWarehouseId = request.MainWarehouseId;
        minPrice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return Ok(minPrice);
    }

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/ProductMinimumSellingPrices/Delete")]
    public async Task<IActionResult> Delete([RouteOrRequest] int id, CancellationToken ct)
    {
        var minPrice = await _context.ProductMinimumSellingPrices.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (minPrice == null) return NotFound("Minimum price configuration not found.");

        _context.ProductMinimumSellingPrices.Remove(minPrice);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public sealed record CreateMinimumPriceRequest(
    int Country,
    int ManufacturingCompanyId,
    int MainWarehouseId,
    decimal MinimumSellingPrice);

public sealed record CountryMinimumPriceRequest(
    int Id,
    int Country,
    int? ManufacturingCompanyId,
    decimal MinimumPriceForOffers,
    decimal? MaximumPriceForOffers);
