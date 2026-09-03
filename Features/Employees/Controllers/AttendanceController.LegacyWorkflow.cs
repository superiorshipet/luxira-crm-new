using System.Text.Json;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

public partial class AttendanceController
{
    private const string DeletedMarker = "[ATTENDANCE_LOG_DELETED]";
    private const string HistoryStart = "[ATTENDANCE_EDIT_HISTORY]";
    private const string HistoryEnd = "[/ATTENDANCE_EDIT_HISTORY]";

    [HttpGet("GetSecureLogoutPolicyStatus")]
    public async Task<IActionResult> GetSecureLogoutPolicyStatus(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var openLog = await _context.EmployeeAttendanceLogs.AsNoTracking()
            .Where(log => log.UserId == userId && log.CheckOutAt == null && (log.Notes == null || !log.Notes.Contains(DeletedMarker)))
            .OrderByDescending(log => log.CheckInAt).Select(log => new { log.Id, log.CheckInAt, log.ShiftEndAt }).FirstOrDefaultAsync(ct);
        var now = IstanbulTimeHelper.Now;
        return Ok(new
        {
            success = true,
            isCheckedIn = openLog is not null,
            attendanceLogId = openLog?.Id,
            canCheckOut = openLog is not null,
            shiftEnded = openLog?.ShiftEndAt is not null && now >= openLog.ShiftEndAt,
            now
        });
    }

    [HttpPost("RegisterWeeklyOffCheckOut")]
    public Task<IActionResult> RegisterWeeklyOffCheckOut(CancellationToken ct) => RegisterLegacyCheckOut(null, null, "WeeklyOff", ct);

    [HttpPost("RegisterCheckOut")]
    public Task<IActionResult> RegisterCheckOut([FromBody] LegacyCheckOutRequest? request, CancellationToken ct) =>
        RegisterLegacyCheckOut(request?.FaceImagePath, request?.Location, request?.Reason, ct);

    [HttpGet("GetAttendanceLogCurrencies")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> GetAttendanceLogCurrencies(string? ids, CancellationToken ct)
    {
        var logIds = ParseIds(ids);
        var rows = await _context.EmployeeAttendanceLogs.AsNoTracking().Where(log => logIds.Contains(log.Id))
            .Join(_context.Employees.AsNoTracking(), log => log.EmployeeId, employee => employee.Id, (log, employee) => employee.Country)
            .Distinct().ToListAsync(ct);
        return Ok(new { success = true, currencies = rows.Select(CurrencyForCountry).Distinct() });
    }

    [HttpPost("UpdateAttendanceLogRow")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> UpdateAttendanceLogRow([FromBody] UpdateAttendanceRowRequest request, CancellationToken ct)
    {
        var log = await _context.EmployeeAttendanceLogs.FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (log is null) return NotFound(new { success = false, message = "السجل غير موجود" });
        var changes = new List<object>();
        var date = request.Date?.Date ?? log.CheckInAt.Date;
        if (TryTime(request.CheckInTime, out var checkIn) && log.CheckInAt != date.Add(checkIn)) { changes.Add(new { fieldName = "وقت الدخول", oldValue = log.CheckInAt, newValue = date.Add(checkIn) }); log.CheckInAt = date.Add(checkIn); }
        if (request.ClearCheckOutTime) { changes.Add(new { fieldName = "وقت الخروج", oldValue = log.CheckOutAt, newValue = (DateTime?)null }); log.CheckOutAt = null; }
        else if (TryTime(request.CheckOutTime, out var checkOut) && log.CheckOutAt != date.Add(checkOut)) { changes.Add(new { fieldName = "وقت الخروج", oldValue = log.CheckOutAt, newValue = date.Add(checkOut) }); log.CheckOutAt = date.Add(checkOut); }
        if (request.DeductionAmount.HasValue && log.DeductionAmount != Math.Max(0, request.DeductionAmount.Value)) { changes.Add(new { fieldName = "الخصم", oldValue = log.DeductionAmount, newValue = request.DeductionAmount }); log.DeductionAmount = Math.Max(0, request.DeductionAmount.Value); }
        if (request.LateReason is not null && log.DeductionReason != request.LateReason.Trim()) { changes.Add(new { fieldName = "سبب الخصم", oldValue = log.DeductionReason, newValue = request.LateReason }); log.DeductionReason = request.LateReason.Trim(); }
        if (changes.Count == 0) return Ok(new { success = true, changed = false });
        var history = JsonSerializer.Serialize(new { editorUserId = User.GetUserId(), editorName = User.Identity?.Name, editedAt = IstanbulTimeHelper.Now, changes });
        log.Notes = (log.Notes ?? string.Empty) + HistoryStart + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(history)) + HistoryEnd;
        log.UpdatedAt = IstanbulTimeHelper.Now;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, changed = true, changesCount = changes.Count });
    }

    [HttpGet("AttendanceEditHistory")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> AttendanceEditHistory(int id, CancellationToken ct)
    {
        var notes = await _context.EmployeeAttendanceLogs.AsNoTracking().Where(log => log.Id == id).Select(log => log.Notes).FirstOrDefaultAsync(ct);
        if (notes is null) return Ok(new { success = true, items = Array.Empty<object>(), totalCount = 0 });
        var items = ParseHistory(notes);
        return Ok(new { success = true, items, totalCount = items.Count });
    }

    [HttpPost("DeleteAttendanceLogRow")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> DeleteAttendanceLogRow([FromBody] AttendanceRowIdRequest request, CancellationToken ct) => await SetDeleted(request.Id, true, ct);

    [HttpGet("DeletedAttendanceLogs")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> DeletedAttendanceLogs(CancellationToken ct)
    {
        var rows = await _context.EmployeeAttendanceLogs.AsNoTracking().Where(log => log.Notes != null && log.Notes.Contains(DeletedMarker))
            .OrderByDescending(log => log.UpdatedAt ?? log.CreatedAt).Take(300).Select(log => new
            {
                log.Id, log.EmployeeId, log.EmployeeName, log.CheckInAt, log.CheckOutAt, log.DeductionAmount, log.DeductionReason,
                DeletedAt = log.UpdatedAt ?? log.CreatedAt
            }).ToListAsync(ct);
        return Ok(new { success = true, rows });
    }

    [HttpPost("RestoreAttendanceLogRow")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> RestoreAttendanceLogRow([FromBody] AttendanceRowIdRequest request, CancellationToken ct) => await SetDeleted(request.Id, false, ct);

    [HttpPost("RestoreAllDeletedAttendanceLogs")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> RestoreAllDeletedAttendanceLogs(CancellationToken ct)
    {
        var logs = await _context.EmployeeAttendanceLogs.Where(log => log.Notes != null && log.Notes.Contains(DeletedMarker)).ToListAsync(ct);
        foreach (var log in logs) { log.Notes = RemoveMarker(log.Notes); log.UpdatedAt = IstanbulTimeHelper.Now; }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, restoredCount = logs.Count });
    }

    private async Task<IActionResult> RegisterLegacyCheckOut(string? faceImagePath, string? location, string? reason, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var log = await _context.EmployeeAttendanceLogs.Where(item => item.UserId == userId && item.CheckOutAt == null && (item.Notes == null || !item.Notes.Contains(DeletedMarker)))
            .OrderByDescending(item => item.CheckInAt).FirstOrDefaultAsync(ct);
        if (log is null) return BadRequest(new { success = false, message = "لا يوجد تسجيل حضور مفتوح." });
        log.CheckOutAt = IstanbulTimeHelper.Now;
        log.CheckOutFaceImagePath = faceImagePath;
        log.CheckOutLocation = location;
        if (!string.IsNullOrWhiteSpace(reason)) log.Notes = string.Join(" | ", new[] { log.Notes, reason }.Where(value => !string.IsNullOrWhiteSpace(value)));
        log.UpdatedAt = IstanbulTimeHelper.Now;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, log.Id, log.CheckOutAt });
    }

    private async Task<IActionResult> SetDeleted(int id, bool deleted, CancellationToken ct)
    {
        var log = await _context.EmployeeAttendanceLogs.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (log is null) return NotFound(new { success = false });
        log.Notes = deleted ? (log.Notes?.Contains(DeletedMarker) == true ? log.Notes : (log.Notes ?? string.Empty) + DeletedMarker) : RemoveMarker(log.Notes);
        log.UpdatedAt = IstanbulTimeHelper.Now;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id, deleted });
    }

    private static List<JsonElement> ParseHistory(string notes)
    {
        var items = new List<JsonElement>();
        var offset = 0;
        while ((offset = notes.IndexOf(HistoryStart, offset, StringComparison.Ordinal)) >= 0)
        {
            var start = offset + HistoryStart.Length;
            var end = notes.IndexOf(HistoryEnd, start, StringComparison.Ordinal);
            if (end < 0) break;
            try { items.Add(JsonSerializer.Deserialize<JsonElement>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(notes[start..end])))); } catch { }
            offset = end + HistoryEnd.Length;
        }
        return items;
    }

    private static string? RemoveMarker(string? notes) => string.IsNullOrWhiteSpace(notes) ? notes : notes.Replace(DeletedMarker, string.Empty).Trim();
    private static bool TryTime(string? value, out TimeSpan time) => TimeSpan.TryParse(value, out time);
    private static List<int> ParseIds(string? value) => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(item => int.TryParse(item, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();
    private static string CurrencyForCountry(string? country) => (country ?? string.Empty).ToLowerInvariant() switch { var value when value.Contains("egypt") || value.Contains("مصر") => "EGP", var value when value.Contains("turkey") || value.Contains("ترك") => "TRY", var value when value.Contains("iraq") || value.Contains("عراق") => "IQD", _ => "USD" };
}

public sealed record LegacyCheckOutRequest(string? FaceImagePath, string? Location, string? Reason);
public sealed record AttendanceRowIdRequest(int Id);
public sealed record UpdateAttendanceRowRequest(int Id, DateTime? Date, string? CheckInTime, string? CheckOutTime, bool ClearCheckOutTime, decimal? DeductionAmount, string? LateReason);
