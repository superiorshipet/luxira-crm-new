using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/screen-records")]
[Route("ScreenRecords")]
[Route("EmployeeActivity")]
public class ScreenRecordsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ScreenRecordsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetRecords")]
    public async Task<ActionResult<List<ScreenRecord>>> GetRecords([FromQuery] int? employeeId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var query = _context.ScreenRecords.AsNoTracking().AsQueryable();
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(s => s.EmployeeId == employeeId.Value);
        }

        if (date.HasValue)
        {
            var dayStart = date.Value.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(s => s.CapturedAt >= dayStart && s.CapturedAt < dayEnd);
        }

        var list = await query.OrderByDescending(s => s.CapturedAt).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [HttpPost("UploadRecord")]
    public async Task<ActionResult<ScreenRecord>> UploadRecord([FromBody] UploadScreenRecordRequest request, CancellationToken ct)
    {
        var rec = new ScreenRecord
        {
            EmployeeId = request.EmployeeId,
            ScreenshotUrl = request.ScreenshotUrl,
            S3Key = request.S3Key,
            ActiveApplication = request.ActiveApplication,
            IdleSeconds = request.IdleSeconds,
            CapturedAt = DateTime.UtcNow
        };

        await _context.ScreenRecords.AddAsync(rec, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(rec);
    }
}

public record UploadScreenRecordRequest(int EmployeeId, string ScreenshotUrl, string? S3Key, string? ActiveApplication, int IdleSeconds);
