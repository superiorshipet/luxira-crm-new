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
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.MainProducts
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .AsQueryable();

        if (manufacturingCompanyId.HasValue)
            query = query.Where(p => p.ManufacturingCompanyId == manufacturingCompanyId.Value);

        var total = await query.CountAsync(ct);
        var productRows = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var products = productRows.Select(ToResponse).ToList();

        return Ok(new { total, page, pageSize, items = products });
    }

    [HttpPost("Create")]
    [HttpPost("/ProductsPrices/Create")]
    public async Task<IActionResult> Create([FromBody] CreateProductPriceRequest request, CancellationToken ct)
    {
        var error = await ValidateRequestAsync(request, null, ct);
        if (error is not null) throw new BadRequestException(error);

        var minimumPrice = request.MinimumSellingPrice ?? request.Price;
        var maximumPrice = request.MaximumSellingPrice ?? minimumPrice;
        var product = new MainProduct
        {
            Name = request.Name.Trim(),
            Country = request.Country,
            Price = minimumPrice,
            MinimumSellingPrice = minimumPrice,
            MaximumSellingPrice = maximumPrice,
            DeliveryPrice = Math.Max(0, request.DeliveryPrice),
            Quantity = request.Quantity <= 0 ? 1 : request.Quantity,
            SaleType = NormalizeSaleType(request.SaleType),
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            ImageUrl = request.ImageUrl,
            IsDeleted = false
        };

        await _context.MainProducts.AddAsync(product, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(ToResponse(product));
    }

    [HttpPost("CreateBulk")]
    [HttpPost("/ProductsPrices/CreateBulk")]
    public async Task<IActionResult> CreateBulk([FromBody] List<CreateProductPriceRequest> items, CancellationToken ct)
    {
        if (items.Count == 0)
            throw new BadRequestException("At least one product is required.");

        if (items.Count > 500)
            throw new BadRequestException("A maximum of 500 products can be created per request.");

        var products = new List<MainProduct>(items.Count);
        foreach (var request in items)
        {
            var error = await ValidateRequestAsync(request, null, ct);
            if (error is not null) throw new BadRequestException(error);

            var minimumPrice = request.MinimumSellingPrice ?? request.Price;
            products.Add(new MainProduct
            {
                Name = request.Name.Trim(),
                Country = request.Country,
                Price = minimumPrice,
                MinimumSellingPrice = minimumPrice,
                MaximumSellingPrice = request.MaximumSellingPrice ?? minimumPrice,
                DeliveryPrice = Math.Max(0, request.DeliveryPrice),
                Quantity = request.Quantity <= 0 ? 1 : request.Quantity,
                SaleType = NormalizeSaleType(request.SaleType),
                ManufacturingCompanyId = request.ManufacturingCompanyId,
                ImageUrl = request.ImageUrl,
                IsDeleted = false
            });
        }

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

        var error = await ValidateRequestAsync(request, id, ct);
        if (error is not null) throw new BadRequestException(error);

        var minimumPrice = request.MinimumSellingPrice ?? request.Price;
        product.Name = request.Name.Trim();
        product.Country = request.Country;
        product.Price = minimumPrice;
        product.MinimumSellingPrice = minimumPrice;
        product.MaximumSellingPrice = request.MaximumSellingPrice ?? minimumPrice;
        product.DeliveryPrice = Math.Max(0, request.DeliveryPrice);
        product.Quantity = request.Quantity <= 0 ? 1 : request.Quantity;
        product.SaleType = NormalizeSaleType(request.SaleType);
        product.ManufacturingCompanyId = request.ManufacturingCompanyId;
        product.ImageUrl = request.ImageUrl ?? product.ImageUrl;

        await _context.SaveChangesAsync(ct);
        return Ok(ToResponse(product));
    }

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/ProductsPrices/Delete")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        product.IsDeleted = true;
        product.DeletedAt = IstanbulTimeHelper.Now;
        product.DeletedByUserId = User.GetUserId();
        product.DeletedByName = User.Identity?.Name;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Product deleted." });
    }

    [HttpPost("Restore/{id:int}")]
    [HttpPost("/ProductsPrices/Restore")]
    public async Task<IActionResult> Restore([FromRoute] int id, CancellationToken ct)
    {
        var product = await _context.MainProducts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product == null) return NotFound("Product not found.");

        product.IsDeleted = false;
        product.DeletedAt = null;
        product.DeletedByUserId = null;
        product.DeletedByName = null;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "Product restored." });
    }

    private async Task<string?> ValidateRequestAsync(
        CreateProductPriceRequest request,
        int? excludedProductId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Product name is required.";
        if (request.Country <= 0) return "Country is required.";
        if (request.ManufacturingCompanyId <= 0) return "Manufacturing company is required.";

        var minimumPrice = request.MinimumSellingPrice ?? request.Price;
        var maximumPrice = request.MaximumSellingPrice ?? minimumPrice;
        if (minimumPrice < 0 || maximumPrice < minimumPrice)
            return "Maximum selling price must be greater than or equal to minimum selling price.";

        if (!await _context.ManufacturingCompanies.AsNoTracking()
                .AnyAsync(company => company.Id == request.ManufacturingCompanyId, ct))
            return "Manufacturing company was not found.";

        var normalizedName = request.Name.Trim();
        var saleType = NormalizeSaleType(request.SaleType);
        var duplicateExists = await _context.MainProducts.AsNoTracking().AnyAsync(
            product => !product.IsDeleted
                && product.Id != excludedProductId
                && product.Name == normalizedName
                && product.Country == request.Country
                && product.ManufacturingCompanyId == request.ManufacturingCompanyId
                && product.SaleType == saleType,
            ct);
        return duplicateExists
            ? "The same product already exists for this country, store, and sale type."
            : null;
    }

    private static string NormalizeSaleType(string? saleType) =>
        string.IsNullOrWhiteSpace(saleType) ? "بيع فردي" : saleType.Trim();

    private static object ToResponse(MainProduct product) => new
    {
        product.Id,
        product.Name,
        product.Country,
        product.Price,
        product.MinimumSellingPrice,
        product.MaximumSellingPrice,
        product.DeliveryPrice,
        product.Quantity,
        product.SaleType,
        product.ImageUrl,
        product.ManufacturingCompanyId,
        product.IsDeleted
    };
}

public record CreateProductPriceRequest(
    string Name,
    decimal Price,
    int ManufacturingCompanyId,
    int Country,
    decimal? MinimumSellingPrice = null,
    decimal? MaximumSellingPrice = null,
    decimal DeliveryPrice = 0,
    int Quantity = 1,
    string? SaleType = null,
    string? ImageUrl = null);
