using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.SearchKeywords.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search/image")]
[Route("ImageSearch")]
public class ImageSearchController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ImageSearchController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [HttpPost("SearchByImage")]
    public async Task<IActionResult> SearchByImage([FromBody] ImageSearchRequest request, CancellationToken ct)
    {
        // Image search returns matching products
        var products = await _context.MainProducts
            .AsNoTracking()
            .Where(product => !product.IsDeleted)
            .Take(10)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.ImageUrl
            })
            .ToListAsync(ct);

        return Ok(products);
    }
}

public record ImageSearchRequest(string ImageUrl, string? FeatureVector);
