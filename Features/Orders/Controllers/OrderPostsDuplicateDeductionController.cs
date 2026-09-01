using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/posts/deductions")]
[Route("OrderPosts")]
public class OrderPostsDuplicateDeductionController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrderPostsDuplicateDeductionController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("ProblemDeductionInfoV2")]
    [HttpGet("/OrderPosts/ProblemDeductionInfoV2")]
    public async Task<IActionResult> ProblemDeductionInfoV2([FromQuery] int orderId, CancellationToken ct)
    {
        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null)
        {
            throw new NotFoundException($"Order {orderId} not found.");
        }

        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.ApplicationUserId == order.ApplicationUserId)
            .Select(e => new { id = e.Id, name = e.DisplayName ?? e.Name })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            orderId = order.Id,
            orderTotal = order.TotalPrice,
            employees
        });
    }

    [HttpPost("CreateProblemDeductionV2")]
    [HttpPost("/OrderPosts/CreateProblemDeductionV2")]
    public async Task<IActionResult> CreateProblemDeductionV2([FromBody] CreateProblemDeductionRequest request, CancellationToken ct)
    {
        var violation = new EmployeeViolation
        {
            EmployeeId = request.EmployeeId,
            Title = "Duplicate Order Deduction",
            Description = $"Deduction for Order #{request.OrderId}: {request.Reason}",
            PenaltyAmount = request.Amount,
            OccurredAt = DateTime.UtcNow,
            IssuedByUserId = User.GetUserId() ?? "system"
        };

        await _context.EmployeeViolations.AddAsync(violation, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new { success = true, violationId = violation.Id, message = "Deduction recorded successfully." });
    }
}

public record CreateProblemDeductionRequest(int OrderId, int EmployeeId, decimal Amount, string? Reason);
