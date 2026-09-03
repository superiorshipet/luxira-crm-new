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

    [HttpPost("MarkAsUnderReview")]
    public async Task<IActionResult> MarkAsUnderReview([FromForm] int id, CancellationToken ct)
    {
        if (!await CanHandleAsync(ct)) return Forbid();
        var report = await _context.UrgentReports.FindAsync([id], ct);
        if (report is null) return NotFound();
        report.Status = "UnderReview";
        report.HandledByAdminName = await CurrentHandlerNameAsync(ct);
        report.HandledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("MarkAsResolved")]
    public async Task<IActionResult> MarkAsResolved([FromForm] int id, CancellationToken ct)
    {
        if (!await CanHandleAsync(ct)) return Forbid();
        var report = await _context.UrgentReports.FindAsync([id], ct);
        if (report is null) return NotFound();
        report.Status = "Resolved";
        report.HandledByAdminName = await CurrentHandlerNameAsync(ct);
        report.HandledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("GetPendingReports")]
    public async Task<IActionResult> GetPendingReports(CancellationToken ct)
    {
        if (!await CanHandleAsync(ct)) return Forbid();
        var reports = await _context.UrgentReports.AsNoTracking()
            .Where(report => report.Status == "Pending" || report.Status == "UnderReview" || report.Status == "Open")
            .OrderByDescending(report => report.CreatedAt)
            .Select(report => new
            {
                report.Id,
                ReportNumber = "#BR-" + report.Id,
                report.ReportType,
                report.Description,
                report.Status,
                report.CreatedAt,
                EmployeeName = report.Employee != null ? report.Employee.Name : "غير معروف"
            }).ToListAsync(ct);
        return Ok(reports);
    }

    [HttpGet("GetAllReports")]
    public async Task<IActionResult> GetAllReports([FromQuery] DateTime? date, CancellationToken ct)
    {
        if (!await CanHandleAsync(ct)) return Forbid();
        var query = _context.UrgentReports.AsNoTracking().AsQueryable();
        if (date.HasValue)
        {
            var start = date.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(report => report.CreatedAt >= start && report.CreatedAt < end);
        }
        return Ok(await query.OrderByDescending(report => report.CreatedAt).Select(report => new
        {
            report.Id,
            report.ReportType,
            report.Description,
            report.ScreenshotPath,
            report.Status,
            report.CreatedAt,
            report.HandledAt,
            report.HandledByAdminName,
            EmployeeName = report.Employee != null ? report.Employee.Name : "غير معروف"
        }).ToListAsync(ct));
    }

    private async Task<bool> CanHandleAsync(CancellationToken ct)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector")) return true;
        var userId = User.GetUserId();
        return userId is not null && await _context.Employees.AsNoTracking()
            .AnyAsync(employee => employee.ApplicationUserId == userId && employee.CanHandleUrgentReports, ct);
    }

    private async Task<string> CurrentHandlerNameAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        return userId is null ? User.Identity?.Name ?? "Admin" : await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => employee.DisplayName ?? employee.Name)
            .FirstOrDefaultAsync(ct) ?? User.Identity?.Name ?? "Admin";
    }
}

public sealed record CreateUrgentReportRequest(
    string ReportType,
    string Description,
    string? ScreenshotPath,
    string? ScreenshotS3Key);
