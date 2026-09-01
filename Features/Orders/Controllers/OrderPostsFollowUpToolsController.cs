using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/posts/follow-up")]
[Route("OrderPosts")]
public class OrderPostsFollowUpToolsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrderPostsFollowUpToolsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("ListFollowUpEditNotes")]
    public async Task<IActionResult> ListFollowUpEditNotes([FromQuery] int orderId, [FromQuery] int type, CancellationToken ct)
    {
        var histories = await _context.OrderEditHistories
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderByDescending(h => h.EditedAt)
            .ToListAsync(ct);

        return Ok(new { success = true, items = histories });
    }

    [HttpGet("ProblemDeductionHistory")]
    public async Task<IActionResult> ProblemDeductionHistory([FromQuery] int orderId, CancellationToken ct)
    {
        var deductions = await _context.EmployeeViolations
            .AsNoTracking()
            .Where(v => v.Description.Contains($"Order #{orderId}"))
            .OrderByDescending(v => v.OccurredAt)
            .ToListAsync(ct);

        return Ok(new { success = true, items = deductions });
    }
}
