using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/posts")]
[Route("OrderPosts")]
public class OrderPostsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;
    private readonly IHubContext<OrderHub> _hub;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OrderPostsController> _logger;

    public OrderPostsController(ApplicationDbContext context, S3StorageService storage, IHubContext<OrderHub> hub, IWebHostEnvironment environment, ILogger<OrderPostsController> logger)
    {
        _context = context;
        _storage = storage;
        _hub = hub;
        _environment = environment;
        _logger = logger;
    }

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

    [HttpPost("Edit")]
    public async Task<IActionResult> Edit([FromForm] int id, [FromForm] string? body, CancellationToken ct)
    {
        var post = await _context.OrderPosts.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (post is null) return NotFound(new { message = "المنشور غير موجود" });
        if (post.AuthorUserId != User.GetUserId()) return Forbid();
        post.Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
        await _context.SaveChangesAsync(ct);
        return Ok(new { id = post.Id, body = post.Body });
    }

    [HttpPost("DeleteImage")]
    public async Task<IActionResult> DeleteImage([FromForm] int imageId, CancellationToken ct)
    {
        var image = await _context.OrderPostImages.Include(item => item.OrderPost).FirstOrDefaultAsync(item => item.Id == imageId, ct);
        if (image is null) return NotFound();
        if (image.OrderPost?.AuthorUserId != User.GetUserId()) return Forbid();
        await DeleteStorageAsync(image, ct);
        _context.OrderPostImages.Remove(image);
        await _context.SaveChangesAsync(ct);
        return Ok(new { imageId });
    }

    [HttpGet("Image")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Image([FromQuery] string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("orderposts/", StringComparison.Ordinal) || key.Contains("..", StringComparison.Ordinal)) return NotFound();
        return Redirect(_storage.GetPresignedUrl(key));
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete([FromForm] int id, CancellationToken ct)
    {
        var post = await _context.OrderPosts.Include(item => item.Images).FirstOrDefaultAsync(item => item.Id == id, ct);
        if (post is null) return NotFound();
        if (post.Type != OrderPostType.OrderNote && !User.IsInRole("Admin") && !User.IsInRole("ExecutiveDirector")) return Forbid();
        foreach (var image in post.Images) await DeleteStorageAsync(image, ct);
        _context.OrderPosts.Remove(post);
        await _context.SaveChangesAsync(ct);
        if (post.Type != OrderPostType.OrderNote)
            await _hub.Clients.Group("OrderPostListeners").SendAsync("newOrderPost", post.OrderId, (int)post.Type, ct);
        return Ok(new { id });
    }

    [HttpGet("Panels")]
    public async Task<IActionResult> Panels(CancellationToken ct)
    {
        if (!CanSeeAll()) return Ok(new { problem = EmptyPanel(), editNote = EmptyPanel() });
        var rows = await _context.OrderPosts.AsNoTracking()
            .Where(post => post.Type == OrderPostType.Problem || post.Type == OrderPostType.EditNote)
            .OrderByDescending(post => post.CreatedAt)
            .Select(post => new { post.OrderId, post.Type, post.CreatedAt, post.AuthorUserId, post.Body, HasImages = post.Images.Any() })
            .ToListAsync(ct);
        var latest = rows.GroupBy(row => new { row.Type, row.OrderId }).Select(group => group.First()).ToList();
        var authorIds = latest.Select(row => row.AuthorUserId).Distinct().ToArray();
        var names = await _context.Users.AsNoTracking().Where(user => authorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Name }).ToDictionaryAsync(user => user.Id, user => user.Name, ct);
        var employeeNames = await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId != null && authorIds.Contains(employee.ApplicationUserId))
            .Select(employee => new { UserId = employee.ApplicationUserId!, employee.DisplayName }).ToDictionaryAsync(employee => employee.UserId, employee => employee.DisplayName, ct);
        var orderIds = latest.Select(row => row.OrderId).Distinct().ToArray();
        var countries = await _context.Orders.AsNoTracking().Where(order => orderIds.Contains(order.Id))
            .Select(order => new { order.Id, order.Country }).ToDictionaryAsync(order => order.Id, order => order.Country, ct);
        object Build(OrderPostType type)
        {
            var typeRows = latest.Where(row => row.Type == type).ToList();
            return new
            {
                count = typeRows.Count,
                groups = typeRows.GroupBy(row => row.CreatedAt.ToString("yyyy-MM-dd")).Select(group => new
                {
                    date = group.Key,
                    cards = group.Select(row => new
                    {
                        orderId = row.OrderId,
                        country = countries.TryGetValue(row.OrderId, out var country) ? country.ToString() : string.Empty,
                        createdAt = row.CreatedAt,
                        authorName = employeeNames.TryGetValue(row.AuthorUserId, out var display) && !string.IsNullOrWhiteSpace(display) ? display : names.GetValueOrDefault(row.AuthorUserId) ?? "غير معروف",
                        snippet = BuildSnippet(row.Body, row.HasImages)
                    }).ToList()
                }).ToList()
            };
        }
        return Ok(new { problem = Build(OrderPostType.Problem), editNote = Build(OrderPostType.EditNote) });
    }

    [HttpGet("OrderCounts")]
    public async Task<IActionResult> OrderCounts([FromQuery] int orderId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var canSeeAll = CanSeeAll();
        var counts = await _context.OrderPosts.AsNoTracking()
            .Where(post => post.OrderId == orderId && (post.Type == OrderPostType.OrderNote || canSeeAll || post.AuthorUserId == userId))
            .GroupBy(post => post.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Type, item => item.Count, ct);
        return Ok(new { problem = counts.GetValueOrDefault(OrderPostType.Problem), editNote = counts.GetValueOrDefault(OrderPostType.EditNote), orderNote = counts.GetValueOrDefault(OrderPostType.OrderNote) });
    }

    private bool CanSeeAll() => User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("FollowUpDepartment") || User.IsInRole("ExecutiveDirector");
    private static object EmptyPanel() => new { count = 0, groups = Array.Empty<object>() };
    private static string BuildSnippet(string? body, bool hasImages)
    {
        if (string.IsNullOrWhiteSpace(body)) return hasImages ? "[صور مرفقة]" : string.Empty;
        var trimmed = body.Trim();
        return trimmed.Length > 120 ? trimmed[..120] + "…" : trimmed;
    }

    private async Task DeleteStorageAsync(OrderPostImage image, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(image.S3Key))
        {
            try { await _storage.DeleteAsync(image.S3Key, ct); }
            catch (Exception ex) { _logger.LogError(ex, "Deleting order-post S3 image failed"); }
        }
        if (string.IsNullOrWhiteSpace(image.Url) || image.Url.Contains("://", StringComparison.Ordinal) || image.Url.StartsWith("/OrderPosts/Image", StringComparison.OrdinalIgnoreCase)) return;
        var root = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"));
        var path = Path.GetFullPath(Path.Combine(root, image.Url.Split('?', '#')[0].TrimStart('/')));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !System.IO.File.Exists(path)) return;
        try { System.IO.File.Delete(path); }
        catch (Exception ex) { _logger.LogError(ex, "Deleting local order-post image failed"); }
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
