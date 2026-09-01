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
[Route("api/v1/orders/urgent-reports")]
[Route("UrgentReport")]
public class UrgentReportController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public UrgentReportController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    [HttpGet("GetReports")]
    public async Task<ActionResult<List<UrgentReport>>> GetReports(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var query = _context.UrgentReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(report => report.Status == status);

        return Ok(await query.OrderByDescending(report => report.CreatedAt).ToListAsync(ct));
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<UrgentReport>> CreateReport(
        [FromBody] CreateUrgentReportRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var employeeId = await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => (int?)employee.Id)
            .FirstOrDefaultAsync(ct);
        if (!employeeId.HasValue)
            return BadRequest(new { message = "The current user is not linked to an employee." });

        var report = new UrgentReport
        {
            ReportType = request.ReportType,
            Description = request.Description,
            ScreenshotPath = request.ScreenshotPath,
            ScreenshotS3Key = request.ScreenshotS3Key,
            EmployeeId = employeeId.Value,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };

        await _context.UrgentReports.AddAsync(report, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(report);
    }

    [HttpPost("{id:int}/resolve")]
    [HttpPost("Resolve/{id:int}")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> ResolveReport([FromRoute] int id, CancellationToken ct)
    {
        var report = await _context.UrgentReports.FindAsync([id], ct);
        if (report is null)
            throw new NotFoundException($"Urgent report {id} not found.");

        report.Status = "Resolved";
        report.HandledByAdminName = User.Identity?.Name;
        report.HandledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Ok(new { message = "Report marked as resolved." });
    }
}

public sealed record CreateUrgentReportRequest(
    string ReportType,
    string Description,
    string? ScreenshotPath,
    string? ScreenshotS3Key);
