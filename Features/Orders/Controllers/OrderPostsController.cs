using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
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

    public OrderPostsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    [HttpGet("List")]
    public async Task<ActionResult<IReadOnlyList<OrderPostDto>>> GetPosts(
        [FromQuery] int orderId,
        [FromQuery] OrderPostType? type,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        var canSeeAll = User.IsInRole("Admin") || User.IsInRole("Administrator") ||
            User.IsInRole("FollowUpDepartment") || User.IsInRole("ExecutiveDirector");

        var query = _context.OrderPosts.AsNoTracking().Where(post => post.OrderId == orderId);
        if (type.HasValue)
        {
            query = query.Where(post => post.Type == type.Value);
            if (type.Value != OrderPostType.OrderNote && !canSeeAll)
                query = query.Where(post => post.AuthorUserId == currentUserId);
        }

        var posts = await query
            .OrderByDescending(post => post.CreatedAt)
            .Select(post => new OrderPostDto(
                post.Id, post.OrderId, post.Type, post.AuthorUserId, post.Body, post.CreatedAt))
            .ToListAsync(ct);

        return Ok(posts);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<OrderPostDto>> CreatePost(
        [FromBody] CreateOrderPostRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Post body is required when image upload is not supplied." });

        var orderExists = await _context.Orders.AsNoTracking()
            .AnyAsync(order => order.Id == request.OrderId, ct);
        if (!orderExists)
            return NotFound(new { message = $"Order {request.OrderId} was not found." });

        var post = new OrderPost
        {
            OrderId = request.OrderId,
            Type = request.Type,
            AuthorUserId = User.GetUserId() ?? "system",
            Body = request.Body.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _context.OrderPosts.AddAsync(post, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new OrderPostDto(
            post.Id, post.OrderId, post.Type, post.AuthorUserId, post.Body, post.CreatedAt));
    }
}

public sealed record OrderPostDto(
    int Id,
    int OrderId,
    OrderPostType Type,
    string AuthorUserId,
    string? Body,
    DateTime CreatedAt);

public sealed record CreateOrderPostRequest(int OrderId, OrderPostType Type, string? Body);
