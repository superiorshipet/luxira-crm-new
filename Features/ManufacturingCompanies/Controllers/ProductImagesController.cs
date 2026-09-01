using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/manufacturing-companies/products/{productId:int}/images")]
[Route("ProductImages")]
public class ProductImagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductImagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("/ProductImages/GetImages/{productId:int}")]
    public async Task<ActionResult<List<ProductImage>>> GetProductImages([FromRoute] int productId, CancellationToken ct)
    {
        var images = await _context.ProductImages
            .AsNoTracking()
            .Where(img => img.MainProductId == productId)
            .ToListAsync(ct);

        return Ok(images);
    }

    [HttpPost]
    [HttpPost("/ProductImages/AddImage")]
    public async Task<ActionResult<ProductImage>> AddImage([FromBody] AddProductImageRequest request, CancellationToken ct)
    {
        var img = new ProductImage
        {
            MainProductId = request.ProductId,
            ImageUrl = request.ImageUrl,
            S3Key = request.S3Key,
            IsPrimary = request.IsPrimary
        };

        await _context.ProductImages.AddAsync(img, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(img);
    }
}

public record AddProductImageRequest(int ProductId, string ImageUrl, string? S3Key, bool IsPrimary);
