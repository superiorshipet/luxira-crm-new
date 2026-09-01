using Luxira.Api.Data;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Employees.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendance")]
[Route("EmployeeAttendance")]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly EmployeeService _service;
    private readonly ApplicationDbContext _context;

    public AttendanceController(EmployeeService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpPost("check-in")]
    [HttpPost("/EmployeeAttendance/CheckIn")]
    public async Task<ActionResult<AttendanceLogDto>> CheckIn([FromBody] CheckInRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _service.CheckInAsync(request, ip, ct);
        return Ok(result);
    }

    [HttpPost("check-out")]
    [HttpPost("/EmployeeAttendance/CheckOut")]
    public async Task<ActionResult<AttendanceLogDto>> CheckOut([FromBody] CheckOutRequest request, CancellationToken ct)
    {
        var result = await _service.CheckOutAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("logs")]
    [HttpGet("/EmployeeAttendance/GetLogs")]
    [HttpGet("/EmployeeAttendance/AttendanceLog")]
    public async Task<ActionResult<List<AttendanceLogDto>>> GetLogs(
        [FromQuery] int? employeeId,
        [FromQuery] DateTime? date,
        CancellationToken ct)
    {
        var logs = await _service.GetAttendanceLogsAsync(employeeId, date, ct);
        return Ok(logs);
    }

    [HttpGet("assign-work-times")]
    [HttpGet("/EmployeeAttendance/AssignWorkTimes")]
    public async Task<IActionResult> GetWorkTimes([FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = _context.EmployeeWorkShifts.AsNoTracking().AsQueryable();
        if (employeeId.HasValue) query = query.Where(s => s.EmployeeId == employeeId.Value);

        var shifts = await query.ToListAsync(ct);
        return Ok(shifts);
    }

    [HttpPost("assign-work-times")]
    [HttpPost("/EmployeeAttendance/AssignWorkTimes")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<IActionResult> SaveWorkTimes([FromBody] List<EmployeeWorkShift> shifts, CancellationToken ct)
    {
        if (shifts.Count > 0)
        {
            var empId = shifts[0].EmployeeId;
            var existing = await _context.EmployeeWorkShifts.Where(s => s.EmployeeId == empId).ToListAsync(ct);
            _context.EmployeeWorkShifts.RemoveRange(existing);
            await _context.EmployeeWorkShifts.AddRangeAsync(shifts, ct);
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { success = true });
    }

    [HttpPost("toggle-login-block")]
    [HttpPost("/EmployeeAttendance/ToggleEmployeeLoginBlock")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<IActionResult> ToggleEmployeeLoginBlock([FromBody] ToggleLoginBlockRequest request, CancellationToken ct)
    {
        var shift = await _context.EmployeeWorkShifts
            .FirstOrDefaultAsync(item => item.Id == request.ShiftId && item.IsActive, ct);
        if (shift is null) throw new NotFoundException("Active employee shift was not found.");

        var now = IstanbulTimeHelper.Now;
        shift.IsLoginBlocked = request.IsBlocked;
        shift.LoginBlockedAt = request.IsBlocked ? now : null;
        shift.LoginBlockReason = request.IsBlocked
            ? "تم عمل بلوك يدوي من الإدارة"
            : "تم فك البلوك يدويًا من الإدارة";
        shift.AdminUnblockedAt = request.IsBlocked ? null : now;
        shift.AdminUnblockedByUserId = request.IsBlocked ? null : User.GetUserId();
        shift.AdminUnblockedUntil = null;
        shift.UpdatedAt = now;

        if (request.IsBlocked)
        {
            var openLog = await _context.EmployeeAttendanceLogs
                .Where(log => log.EmployeeId == shift.EmployeeId && log.CheckOutAt == null)
                .OrderByDescending(log => log.CheckInAt)
                .FirstOrDefaultAsync(ct);
            if (openLog is not null) openLog.CheckOutAt = now;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, shiftId = shift.Id, isBlocked = shift.IsLoginBlocked });
    }

    [HttpGet("live-logs")]
    [HttpGet("/EmployeeAttendance/GetAttendanceLogLiveVersion")]
    public async Task<IActionResult> GetAttendanceLogLiveVersion(CancellationToken ct)
    {
        var today = IstanbulTimeHelper.Now.Date;
        var logs = await _context.EmployeeAttendanceLogs
            .Include(a => a.Employee)
            .Where(a => a.CheckInAt >= today)
            .OrderByDescending(a => a.CheckInAt)
            .Take(50)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(logs);
    }

    [HttpGet("capture-status")]
    [HttpGet("/EmployeeAttendance/GetCheckInCaptureStatus")]
    public async Task<IActionResult> GetCheckInCaptureStatus(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var employee = await _context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ApplicationUserId == userId, ct);
        if (employee is null)
            return Ok(new { shouldCapture = false, hasOpenLog = false, reason = "employee_not_found" });

        var hasOpenLog = await _context.EmployeeAttendanceLogs.AsNoTracking()
            .AnyAsync(log => log.EmployeeId == employee.Id && log.CheckOutAt == null, ct);
        return Ok(new
        {
            shouldCapture = !hasOpenLog,
            requireFaceVerification = employee.HasFacePrint,
            hasOpenLog
        });
    }

    [HttpGet("late-notifications")]
    [HttpGet("/EmployeeAttendance/GetLateAttendanceNotification")]
    public async Task<IActionResult> GetLateAttendanceNotification(CancellationToken ct)
    {
        var today = IstanbulTimeHelper.Now.Date;
        var logs = await _context.EmployeeAttendanceLogs
            .Include(a => a.Employee)
            .Where(a => a.CheckInAt >= today && a.CheckInAt.TimeOfDay > new TimeSpan(9, 30, 0))
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(logs);
    }

    [HttpGet("absent-notifications")]
    [HttpGet("/EmployeeAttendance/GetAbsentAttendanceNotification")]
    public async Task<IActionResult> GetAbsentAttendanceNotification(CancellationToken ct)
    {
        var today = IstanbulTimeHelper.Now.Date;
        var checkedInEmpIds = await _context.EmployeeAttendanceLogs
            .Where(a => a.CheckInAt >= today)
            .Select(a => a.EmployeeId)
            .Distinct()
            .ToListAsync(ct);

        var absentees = await _context.Employees
            .Where(e => e.IsActive && !checkedInEmpIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name, e.PhoneNumber, e.JobTitle })
            .ToListAsync(ct);

        return Ok(absentees);
    }

    [HttpPost("register-secure-checkin")]
    [HttpPost("/EmployeeAttendance/RegisterCheckIn")]
    public async Task<IActionResult> RegisterCheckIn([FromBody] CheckInRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _service.CheckInAsync(request, ip, ct);
        return Ok(new { success = true, attendance = result });
    }

    [HttpPost("register-question-checkin")]
    [HttpPost("/EmployeeAttendance/RegisterQuestionCheckIn")]
    public async Task<IActionResult> RegisterQuestionCheckIn([FromBody] CheckInRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _service.CheckInAsync(request, ip, ct);
        return Ok(new { success = true, attendance = result, method = "security_question" });
    }
}

public record ToggleLoginBlockRequest(int ShiftId, bool IsBlocked);
