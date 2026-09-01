using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/activity")]
[Route("EmployeeActivity")]
public class EmployeeActivityController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeeActivityController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("LoginSessions")]
    [HttpGet("/EmployeeActivity/LoginSessions")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<IActionResult> LoginSessions([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct = default)
    {
        var from = fromDate ?? IstanbulTimeHelper.Now.Date;
        var to = toDate ?? from.AddDays(1);

        var sessions = await _context.EmployeeAttendanceLogs
            .Include(a => a.Employee)
            .Where(a => a.CheckIn >= from && a.CheckIn <= to)
            .OrderByDescending(a => a.CheckIn)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(sessions);
    }

    [HttpGet("LiveStatuses")]
    [HttpGet("/EmployeeActivity/LiveStatuses")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<IActionResult> LiveStatuses([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct = default)
    {
        var now = IstanbulTimeHelper.Now;
        var fiveMinutesAgo = now.AddMinutes(-5);

        var activeEmployees = await _context.EmployeeActivityLogs
            .Include(a => a.Employee)
            .Where(a => a.Timestamp >= fiveMinutesAgo)
            .GroupBy(a => a.EmployeeId)
            .Select(g => g.OrderByDescending(x => x.Timestamp).First())
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(activeEmployees);
    }

    [HttpGet("HourlyStatus")]
    [HttpGet("/EmployeeActivity/HourlyStatus")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<IActionResult> HourlyStatus([FromQuery] int employeeId, [FromQuery] DateTime activityDate, [FromQuery] string time, CancellationToken ct = default)
    {
        var logs = await _context.EmployeeActivityLogs
            .Where(a => a.EmployeeId == employeeId && a.Timestamp.Date == activityDate.Date)
            .OrderBy(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(logs);
    }

    [HttpPost("Heartbeat")]
    [HttpPost("/EmployeeActivity/Heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] EmployeeActivityHeartbeatRequest request, CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId || e.Id == request.EmployeeId, ct);
        if (employee == null) return NotFound("Employee not found.");

        var log = new EmployeeActivityLog
        {
            EmployeeId = employee.Id,
            ActivityType = request.ActivityType ?? "Online",
            Details = request.Details ?? "Browser active",
            Timestamp = IstanbulTimeHelper.Now
        };

        await _context.EmployeeActivityLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, timestamp = log.Timestamp });
    }
}

public record EmployeeActivityHeartbeatRequest(int? EmployeeId, string? ActivityType, string? Details);
