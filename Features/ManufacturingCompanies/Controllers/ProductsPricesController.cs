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
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant,ManufacturingCompany")]
[Route("api/v1/manufacturing/products-prices")]
[Route("ProductsPrices")]
public class ProductsPricesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsPricesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/ProductsPrices/Index")]
    public async Task<IActionResult> Index(
        [FromQuery] int? manufacturingCompanyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.MainProducts
            .Include(p => p.ManufacturingCompany)
            .Include(p => p.Images)
            .Where(p => p.IsActive)
            .AsNoTracking()
            .AsQueryable();

        if (manufacturingCompanyId.HasValue)
            query = query.Where(p => p.ManufacturingCompanyId == manufacturingCompanyId.Value);

        var total = await query.CountAsync(ct);
        var products = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items = products });
    }

    [HttpPost("Create")]
    [HttpPost("/ProductsPrices/Create")]
    public async Task<IActionResult> Create([FromBody] CreateProductPriceRequest request, CancellationToken ct)
    {
        var product = new MainProduct
        {
            Name = request.Name,
            SKU = request.SKU,
            DefaultPrice = request.Price,
            DefaultCost = request.Cost,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            IsActive = true
        };

        if (!string.IsNullOrEmpty(request.ImageUrl))
        {
            product.Images.Add(new ProductImage
            {
                ImageUrl = request.ImageUrl,
                IsPrimary = true
            });
        }

        await _context.MainProducts.AddAsync(product, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(product);
    }

    [HttpPost("CreateBulk")]
    [HttpPost("/ProductsPrices/CreateBulk")]
    public async Task<IActionResult> CreateBulk([FromBody] List<CreateProductPriceRequest> items, CancellationToken ct)
    {
        var products = items.Select(req => new MainProduct
        {
            Name = req.Name,
            SKU = req.SKU,
            DefaultPrice = req.Price,
            DefaultCost = req.Cost,
            ManufacturingCompanyId = req.ManufacturingCompanyId,
            IsActive = true
        }).ToList();

        await _context.MainProducts.AddRangeAsync(products, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, createdCount = products.Count });
    }

    [HttpPost("Edit/{id:int}")]
    [HttpPut("{id:int}")]
    [HttpPost("/ProductsPrices/Edit")]
    public async Task<IActionResult> Edit([FromRoute] int id, [FromBody] CreateProductPriceRequest request, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        product.Name = request.Name;
        product.SKU = request.SKU ?? product.SKU;
        product.DefaultPrice = request.Price;
        product.DefaultCost = request.Cost ?? product.DefaultCost;
        product.ManufacturingCompanyId = request.ManufacturingCompanyId;

        await _context.SaveChangesAsync(ct);
        return Ok(product);
    }

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/ProductsPrices/Delete")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        product.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Product deleted." });
    }

    [HttpPost("Restore/{id:int}")]
    [HttpPost("/ProductsPrices/Restore")]
    public async Task<IActionResult> Restore([FromRoute] int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        product.IsActive = true;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Product restored." });
    }
}

public record CreateProductPriceRequest(string Name, string? SKU, decimal Price, decimal? Cost, int ManufacturingCompanyId, string? ImageUrl);
