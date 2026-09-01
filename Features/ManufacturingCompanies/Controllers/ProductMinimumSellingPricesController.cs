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
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var prices = await _context.ProductMinimumSellingPrices.AsNoTracking().ToListAsync(ct);
        return Ok(prices);
    }

    [HttpPost("Create")]
    [HttpPost("/CountryMinimumPrices/Create")]
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
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var prices = await _context.ProductMinimumSellingPrices.AsNoTracking().ToListAsync(ct);
        return Ok(prices);
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
    public async Task<IActionResult> Edit([FromRoute] int id, [FromBody] CreateMinimumPriceRequest request, CancellationToken ct)
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
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
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
