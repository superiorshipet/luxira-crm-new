using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/posts")]
[Route("OrderPosts")]
public class OrderPostsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrderPostsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetPosts")]
    public async Task<ActionResult<List<OrderPostDto>>> GetPosts([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.OrderPosts.AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            query = query.Where(p => p.Country == country.Value);
        }

        var posts = await query.OrderByDescending(p => p.CreatedDate)
            .Select(p => new OrderPostDto(p.Id, p.Country, p.PostId, p.PostUrl, p.Title, p.StoreName, p.IsActive, p.CreatedDate))
            .ToListAsync(ct);

        return Ok(posts);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<OrderPostDto>> CreatePost([FromBody] CreateOrderPostRequest request, CancellationToken ct)
    {
        var post = new OrderPost
        {
            Country = request.Country,
            PostId = request.PostId,
            PostUrl = request.PostUrl,
            Title = request.Title,
            StoreName = request.StoreName,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        await _context.OrderPosts.AddAsync(post, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new OrderPostDto(post.Id, post.Country, post.PostId, post.PostUrl, post.Title, post.StoreName, post.IsActive, post.CreatedDate));
    }
}

public record OrderPostDto(int Id, int Country, string PostId, string? PostUrl, string Title, string? StoreName, bool IsActive, DateTime CreatedDate);
public record CreateOrderPostRequest(int Country, string PostId, string? PostUrl, string Title, string? StoreName);
