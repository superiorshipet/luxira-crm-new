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

    public UrgentReportController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetReports")]
    public async Task<ActionResult<List<UrgentReportDto>>> GetReports([FromQuery] string? status, CancellationToken ct)
    {
        var query = _context.UrgentReports
            .Include(r => r.Order)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var reports = await query.OrderByDescending(r => r.CreatedAt)
            .Select(r => new UrgentReportDto(
                r.Id,
                r.OrderId,
                r.Order != null ? r.Order.CustomerName : null,
                r.Title,
                r.Description,
                r.Priority,
                r.Status,
                r.ReportedByUserId,
                r.AssignedToUserId,
                r.ResolutionNote,
                r.CreatedAt,
                r.ResolvedAt))
            .ToListAsync(ct);

        return Ok(reports);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<UrgentReportDto>> CreateReport([FromBody] CreateUrgentReportRequest request, CancellationToken ct)
    {
        var report = new UrgentReport
        {
            OrderId = request.OrderId,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority ?? "High",
            Status = "Open",
            ReportedByUserId = User.GetUserId() ?? "system",
            CreatedAt = DateTime.UtcNow
        };

        await _context.UrgentReports.AddAsync(report, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new UrgentReportDto(
            report.Id,
            report.OrderId,
            null,
            report.Title,
            report.Description,
            report.Priority,
            report.Status,
            report.ReportedByUserId,
            null,
            null,
            report.CreatedAt,
            null));
    }

    [HttpPost("{id:int}/resolve")]
    [HttpPost("Resolve/{id:int}")]
    public async Task<IActionResult> ResolveReport([FromRoute] int id, [FromBody] ResolveUrgentReportRequest request, CancellationToken ct)
    {
        var report = await _context.UrgentReports.FindAsync([id], ct);
        if (report == null)
        {
            throw new NotFoundException($"Urgent report {id} not found.");
        }

        report.Status = "Resolved";
        report.ResolutionNote = request.ResolutionNote;
        report.ResolvedAt = DateTime.UtcNow;
        report.AssignedToUserId = User.GetUserId();

        await _context.SaveChangesAsync(ct);
        return Ok(new { message = "Report marked as resolved." });
    }
}

public record UrgentReportDto(int Id, int? OrderId, string? CustomerName, string Title, string Description, string Priority, string Status, string ReportedByUserId, string? AssignedToUserId, string? ResolutionNote, DateTime CreatedAt, DateTime? ResolvedAt);
public record CreateUrgentReportRequest(int? OrderId, string Title, string Description, string? Priority);
public record ResolveUrgentReportRequest(string ResolutionNote);
