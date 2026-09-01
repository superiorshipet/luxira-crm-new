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
    public async Task<ActionResult<List<ScreenRecord>>> GetRecords([FromQuery] string? employeeId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var query = _context.ScreenRecords.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            query = query.Where(record => record.EmployeeId == employeeId);
        }

        if (date.HasValue)
        {
            var dayStart = date.Value.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(record => record.Date >= dayStart && record.Date < dayEnd);
        }

        var list = await query.OrderByDescending(record => record.Date).ThenByDescending(record => record.StartTime).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [HttpPost("UploadRecord")]
    public async Task<ActionResult<ScreenRecord>> UploadRecord([FromBody] UploadScreenRecordRequest request, CancellationToken ct)
    {
        var rec = new ScreenRecord
        {
            EmployeeId = request.EmployeeId,
            Date = request.Date.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            VideoPath = request.VideoPath,
            VideoS3Key = request.VideoS3Key,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ScreenRecords.AddAsync(rec, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(rec);
    }
}

public record UploadScreenRecordRequest(
    string EmployeeId,
    DateTime Date,
    DateTime StartTime,
    DateTime? EndTime,
    string VideoPath,
    string? VideoS3Key);
