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
[Route("api/v1/employees/breaks")]
[Route("Break")]
public class BreakController : ControllerBase
{
    private const string BreakStartMarker = "[BREAK_START]";
    private const string BreakEndMarker = "[BREAK_END]";
    private readonly ApplicationDbContext _context;

    public BreakController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("status")]
    [HttpGet("GetBreakStatus")]
    public async Task<ActionResult<EmployeeBreakDto>> GetStatus(CancellationToken ct)
    {
        var (_, attendance) = await GetCurrentAttendanceAsync(ct);
        return Ok(Map(attendance));
    }

    [HttpPost("start")]
    public async Task<ActionResult<EmployeeBreakDto>> StartBreak(
        [FromBody] StartBreakRequest request,
        CancellationToken ct)
    {
        var (_, attendance) = await GetCurrentAttendanceAsync(ct);
        if (attendance.BreakStartAt.HasValue)
        {
            return Ok(Map(attendance));
        }

        var now = IstanbulTimeHelper.Now;
        attendance.BreakStartAt = now;
        attendance.UpdatedAt = now;
        attendance.Notes = AppendNote(
            attendance.Notes,
            $"{BreakStartMarker} {request.Reason}".Trim());
        await _context.SaveChangesAsync(ct);
        return Ok(Map(attendance));
    }

    [HttpPost("{id:int}/end")]
    [HttpPost("EndBreak")]
    public async Task<ActionResult<EmployeeBreakDto>> EndBreak(
        [FromRoute] int? id,
        CancellationToken ct)
    {
        var (_, attendance) = await GetCurrentAttendanceAsync(ct);
        if (id.HasValue && id.Value > 0 && attendance.Id != id.Value)
        {
            throw new ForbidException("The attendance record does not belong to the current active session.");
        }

        if (!attendance.BreakStartAt.HasValue)
        {
            throw new BadRequestException("No active break was found.");
        }

        var now = IstanbulTimeHelper.Now;
        var startedAt = attendance.BreakStartAt.Value;
        attendance.BreakStartAt = null;
        attendance.UpdatedAt = now;
        attendance.Notes = AppendNote(
            attendance.Notes,
            $"{BreakEndMarker} DurationSeconds:{Math.Max(0, (long)(now - startedAt).TotalSeconds)}");
        await _context.SaveChangesAsync(ct);
        return Ok(Map(attendance, startedAt, now));
    }

    private async Task<(Employee Employee, EmployeeAttendanceLog Attendance)>
        GetCurrentAttendanceAsync(CancellationToken ct)
    {
        var userId = User.GetUserId() ??
            throw new UnauthorizedException("Authenticated user identifier is missing.");
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ApplicationUserId == userId && item.IsActive, ct) ??
            throw new NotFoundException("Active employee profile was not found.");

        var since = IstanbulTimeHelper.Now.AddHours(-24);
        var attendance = await _context.EmployeeAttendanceLogs
            .Where(log =>
                log.EmployeeId == employee.Id &&
                log.CheckOutAt == null &&
                log.CheckInAt >= since)
            .OrderByDescending(log => log.CheckInAt)
            .FirstOrDefaultAsync(ct) ??
            throw new BadRequestException("No active attendance record was found.");

        return (employee, attendance);
    }

    private static string AppendNote(string? current, string value) =>
        string.IsNullOrWhiteSpace(current) ? value : $"{current} | {value}";

    private static EmployeeBreakDto Map(
        EmployeeAttendanceLog attendance,
        DateTime? startedAt = null,
        DateTime? endedAt = null) =>
        new(
            attendance.Id,
            attendance.EmployeeId ?? 0,
            startedAt ?? attendance.BreakStartAt,
            endedAt,
            attendance.BreakStartAt.HasValue,
            attendance.Notes);
}

public record EmployeeBreakDto(
    int AttendanceLogId,
    int EmployeeId,
    DateTime? StartTime,
    DateTime? EndTime,
    bool IsActive,
    string? Notes);
public record StartBreakRequest(string? Reason);
