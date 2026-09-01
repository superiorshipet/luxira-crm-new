using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/product-images")]
[Route("ProductImages")]
public class ProductImagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductImagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("by-product/{productId:int}")]
    [HttpGet("/ProductImages/GetImages/{productId:int}")]
    public async Task<ActionResult<List<ProductImage>>> GetProductImages(
        [FromRoute] int productId,
        CancellationToken ct)
    {
        var product = await _context.MainProducts.AsNoTracking()
            .Where(item => item.Id == productId)
            .Select(item => new { item.Name, item.ManufacturingCompanyId })
            .FirstOrDefaultAsync(ct);
        if (product is null) throw new NotFoundException("Product not found.");

        var images = await _context.ProductImages
            .AsNoTracking()
            .Where(image => image.ManufacturingCompanyId == product.ManufacturingCompanyId
                && image.ProductName == product.Name)
            .OrderByDescending(image => image.CreatedAt)
            .ToListAsync(ct);

        return Ok(images);
    }

    [HttpPost]
    [HttpPost("/ProductImages/AddImage")]
    public async Task<ActionResult<ProductImage>> AddImage([FromBody] AddProductImageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            throw new BadRequestException("Image URL is required.");
        if (string.IsNullOrWhiteSpace(request.ProductName))
            throw new BadRequestException("Product name is required.");
        if (!await _context.ManufacturingCompanies.AsNoTracking()
                .AnyAsync(company => company.Id == request.ManufacturingCompanyId, ct))
            throw new NotFoundException("Manufacturing company not found.");

        var img = new ProductImage
        {
            ImageUrl = request.ImageUrl.Trim(),
            ProductName = request.ProductName.Trim(),
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            CreatedAt = IstanbulTimeHelper.Now,
            CreatedByUserId = User.GetUserId(),
            CreatedByName = User.Identity?.Name
        };

        await _context.ProductImages.AddAsync(img, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(img);
    }
}

public record AddProductImageRequest(string ProductName, int ManufacturingCompanyId, string ImageUrl);
