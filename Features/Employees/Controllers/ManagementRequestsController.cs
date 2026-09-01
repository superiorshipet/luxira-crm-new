using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/management-requests")]
[Route("ManagementRequests")]
public class ManagementRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ManagementRequestsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetRequests")]
    public async Task<ActionResult<List<ManagementRequestDto>>> GetRequests([FromQuery] int? employeeId, [FromQuery] string? status, CancellationToken ct)
    {
        var query = _context.ManagementRequests
            .Include(m => m.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(m => m.EmployeeId == employeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(m => m.Status == status);
        }

        var list = await query.OrderByDescending(m => m.CreatedAt)
            .Select(m => new ManagementRequestDto(
                m.Id,
                m.EmployeeId,
                m.Employee != null ? m.Employee.Name : null,
                m.RequestType,
                m.Title,
                m.Description,
                m.RequestedAmount,
                m.StartDate,
                m.EndDate,
                m.Status,
                m.ManagerFeedback,
                m.ReviewedByUserId,
                m.CreatedAt,
                m.ReviewedAt))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [HttpPost("SubmitRequest")]
    public async Task<ActionResult<ManagementRequestDto>> SubmitRequest([FromBody] SubmitManagementRequest request, CancellationToken ct)
    {
        var item = new ManagementRequest
        {
            EmployeeId = request.EmployeeId,
            RequestType = request.RequestType,
            Title = request.Title,
            Description = request.Description,
            RequestedAmount = request.RequestedAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _context.ManagementRequests.AddAsync(item, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new ManagementRequestDto(
            item.Id,
            item.EmployeeId,
            null,
            item.RequestType,
            item.Title,
            item.Description,
            item.RequestedAmount,
            item.StartDate,
            item.EndDate,
            item.Status,
            null,
            null,
            item.CreatedAt,
            null));
    }

    [HttpPost("{id:int}/review")]
    [HttpPost("ReviewRequest/{id:int}")]
    public async Task<IActionResult> ReviewRequest([FromRoute] int id, [FromBody] ReviewManagementRequest request, CancellationToken ct)
    {
        var item = await _context.ManagementRequests.FindAsync([id], ct);
        if (item == null)
        {
            throw new NotFoundException($"Management request {id} not found.");
        }

        item.Status = request.Approved ? "Approved" : "Rejected";
        item.ManagerFeedback = request.Feedback;
        item.ReviewedByUserId = User.GetUserId();
        item.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return Ok(new { status = item.Status, message = $"Request marked as {item.Status}." });
    }
}

public record ManagementRequestDto(
    int Id,
    int EmployeeId,
    string? EmployeeName,
    string RequestType,
    string Title,
    string Description,
    decimal? RequestedAmount,
    DateTime? StartDate,
    DateTime? EndDate,
    string Status,
    string? ManagerFeedback,
    string? ReviewedByUserId,
    DateTime CreatedAt,
    DateTime? ReviewedAt
);

public record SubmitManagementRequest(
    int EmployeeId,
    string RequestType,
    string Title,
    string Description,
    decimal? RequestedAmount,
    DateTime? StartDate,
    DateTime? EndDate
);

public record ReviewManagementRequest(bool Approved, string? Feedback);
