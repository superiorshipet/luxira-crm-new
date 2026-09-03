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
    public ManagementRequestsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    [HttpGet("GetRequests")]
    public async Task<ActionResult<List<ManagementRequest>>> GetRequests(
        [FromQuery] string? applicationUserId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var query = _context.ManagementRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(applicationUserId))
            query = query.Where(request => request.ApplicationUserId == applicationUserId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(request => request.Status == status);

        return Ok(await query.OrderByDescending(request => request.CreatedAt).ToListAsync(ct));
    }

    [HttpPost]
    [HttpPost("SubmitRequest")]
    public async Task<ActionResult<ManagementRequest>> SubmitRequest(
        [FromBody] SubmitManagementRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var employee = await _context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ApplicationUserId == userId, ct);

        var item = new ManagementRequest
        {
            ApplicationUserId = userId,
            EmployeeName = employee?.DisplayName ?? employee?.Name ?? User.Identity?.Name ?? string.Empty,
            EmployeeEmail = User.Identity?.Name ?? string.Empty,
            RequestType = request.RequestType,
            Reason = request.Reason,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _context.ManagementRequests.AddAsync(item, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpPost("{id:int}/review")]
    [HttpPost("ReviewRequest/{id:int}")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<IActionResult> ReviewRequest(
        [RouteOrRequest] int id,
        [FromBody] ReviewManagementRequest request,
        CancellationToken ct)
    {
        var item = await _context.ManagementRequests.FindAsync([id], ct);
        if (item is null)
            throw new NotFoundException($"Management request {id} not found.");

        item.Status = request.Approved ? "Approved" : "Rejected";
        item.DecidedByUserId = User.GetUserId();
        item.DecidedByName = User.Identity?.Name;
        item.DecidedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new { status = item.Status });
    }
}

public sealed record SubmitManagementRequest(string RequestType, string Reason);
public sealed record ReviewManagementRequest(bool Approved);
