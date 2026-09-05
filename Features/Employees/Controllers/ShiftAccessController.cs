using System.Net;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/shift-access")]
[Route("ShiftAccess")]
public sealed class ShiftAccessController(ApplicationDbContext context) : ControllerBase
{
    private const string AttendanceDeletedNoteMarker = "[AttendanceDeleted]";

    [HttpGet("CheckCurrentShift")]
    [HttpGet("/ShiftAccess/CheckCurrentShift")]
    public async Task<IActionResult> CheckCurrentShift(CancellationToken ct = default)
    {
        try
        {
            if (ShouldSkipShiftAccess()) return AccessResult();

            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Ok(new { success = false, shouldLogout = false, message = "لم يتم العثور على المستخدم الحالي" });

            var employee = await context.Employees.AsNoTracking()
                .Where(item => item.ApplicationUserId == userId)
                .Select(item => new { item.Id, item.ApplyShiftAccess })
                .FirstOrDefaultAsync(ct);
            if (employee is null || !employee.ApplyShiftAccess) return AccessResult();

            var shift = await context.EmployeeWorkShifts
                .Where(item => item.EmployeeId == employee.Id && item.IsActive)
                .OrderByDescending(item => item.Id)
                .FirstOrDefaultAsync(ct);
            if (shift is null) return AccessResult();

            var now = CairoNow();
            var window = BuildShiftWindow(now, shift.ShiftStartTime, shift.ShiftEndTime);
            var insideAttendanceWindow = now >= window.AccessStart && now < window.EndWithGrace;
            var beforeShiftStart = now < window.Start;
            var afterGrace = now >= window.EndWithGrace;

            if (shift.AdminUnblockedUntil > now)
            {
                if (shift.IsLoginBlocked)
                {
                    shift.IsLoginBlocked = false;
                    shift.UpdatedAt = now;
                    await context.SaveChangesAsync(ct);
                }
                MarkPreShiftAccessIfNeeded(shift, now, window);
                return AccessResult();
            }

            if (shift.IsLoginBlocked)
            {
                if (IsManualLoginBlock(shift))
                    return AccessResult("تم إيقاف دخولك من الإدارة. يجب أن تقوم الإدارة بفتح الدخول أولاً.");

                if (insideAttendanceWindow)
                {
                    shift.IsLoginBlocked = false;
                    shift.LoginBlockedAt = null;
                    shift.LoginBlockReason = null;
                    shift.UpdatedAt = now;
                    await context.SaveChangesAsync(ct);
                }
                else return AccessResult(BuildOutsideShiftMessage(window, afterGrace));
            }

            if (beforeShiftStart)
            {
                MarkPreShiftAccessIfNeeded(shift, now, window);
                return AccessResult();
            }

            if (insideAttendanceWindow && HasPreShiftAccessCookie(shift, window))
            {
                var hasAttendance = await HasCompletedAttendanceForShiftAsync(userId, employee.Id, window, ct);
                Response.Cookies.Delete(PreShiftCookieName(shift));
                if (!hasAttendance)
                    return AccessResult("بدأ موعد دوامك. من فضلك ادخل مرة أخرى حتى يتم تسجيل الحضور بصورة الدخول أو السؤال.");
            }

            if (afterGrace)
            {
                await CloseOpenAttendanceAsync(userId, employee.Id, now, "تسجيل خروج تلقائي بعد نهاية الشيفت بنصف ساعة", ct);
                await context.SaveChangesAsync(ct);
            }

            return AccessResult();
        }
        catch
        {
            return Ok(new { success = false, shouldLogout = false, message = "تعذر التحقق من موعد الدوام." });
        }
    }

    private bool ShouldSkipShiftAccess() =>
        User.IsInRole("Admin") || User.IsInRole("Administrator") ||
        User.IsInRole("ExecutiveDirector") || User.IsInRole("DeliveryCompany") ||
        User.IsInRole("DeliveryRepresentative") || User.IsInRole("OrderPreparer");

    private OkObjectResult AccessResult(string message = "") =>
        Ok(new { success = true, shouldLogout = false, message });

    private static bool IsManualLoginBlock(EmployeeWorkShift shift)
    {
        var reason = shift.LoginBlockReason ?? string.Empty;
        return reason.Contains("يدوي", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("الإدارة", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("الادارة", StringComparison.OrdinalIgnoreCase);
    }

    private static string PreShiftCookieName(EmployeeWorkShift shift) => $"LuxiraPreShiftAccess_{shift.Id}";
    private static string PreShiftCookieValue(ShiftWindow window) => window.Start.ToString("yyyyMMddHHmm");

    private bool HasPreShiftAccessCookie(EmployeeWorkShift shift, ShiftWindow window) =>
        Request.Cookies.TryGetValue(PreShiftCookieName(shift), out var value) &&
        string.Equals(value, PreShiftCookieValue(window), StringComparison.OrdinalIgnoreCase);

    private void MarkPreShiftAccessIfNeeded(EmployeeWorkShift shift, DateTime now, ShiftWindow window)
    {
        if (now >= window.Start) return;
        Response.Cookies.Append(PreShiftCookieName(shift), PreShiftCookieValue(window), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddHours(12), IsEssential = true,
            SameSite = SameSiteMode.Lax, Secure = Request.IsHttps,
        });
    }

    private Task<bool> HasCompletedAttendanceForShiftAsync(string userId, int employeeId, ShiftWindow window, CancellationToken ct) =>
        context.EmployeeAttendanceLogs.AsNoTracking().AnyAsync(log =>
            log.UserId == userId && log.EmployeeId == employeeId &&
            log.CheckInAt >= window.AccessStart && log.CheckInAt <= window.EndWithGrace &&
            (log.Notes == null || !log.Notes.Contains(AttendanceDeletedNoteMarker)) &&
            (log.Notes == null || !log.Notes.Contains("AutoAbsent")) &&
            ((log.FaceImagePath != null && log.FaceImagePath != "") ||
             (log.Notes != null && (log.Notes.Contains("QuestionCheckIn") ||
              log.Notes.Contains("تسجيل الحضور بسؤال") || log.Notes.Contains("جاهز لبدء الدوام")))), ct);

    private async Task CloseOpenAttendanceAsync(string userId, int employeeId, DateTime checkOutAt, string reason, CancellationToken ct)
    {
        var openLog = await context.EmployeeAttendanceLogs
            .Where(log => log.UserId == userId && log.EmployeeId == employeeId && log.CheckOutAt == null)
            .OrderByDescending(log => log.CheckInAt).FirstOrDefaultAsync(ct);
        if (openLog is null) return;
        openLog.CheckOutAt = checkOutAt;
        openLog.CheckOutIpAddress = CurrentIpAddress();
        openLog.CheckOutLocation = reason;
        openLog.UpdatedAt = checkOutAt;
        if (string.IsNullOrWhiteSpace(openLog.Notes)) openLog.Notes = reason;
        else if (!openLog.Notes.Contains(reason, StringComparison.Ordinal)) openLog.Notes += " - " + reason;
    }

    private string CurrentIpAddress()
    {
        var value = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
            ?? Request.Headers["X-Real-IP"].FirstOrDefault()?.Trim();
        if (!IPAddress.TryParse(value, out var parsed)) parsed = HttpContext.Connection.RemoteIpAddress;
        if (parsed is null) return string.Empty;
        if (parsed.IsIPv4MappedToIPv6) parsed = parsed.MapToIPv4();
        return IPAddress.IsLoopback(parsed) ? "127.0.0.1" : parsed.ToString();
    }

    private static ShiftWindow BuildShiftWindow(DateTime now, TimeSpan startTime, TimeSpan endTime)
    {
        var start = now.Date.Add(startTime);
        if (endTime > startTime)
        {
            var end = now.Date.Add(endTime);
            return new(start, start.AddMinutes(-30), end, end.AddMinutes(30));
        }
        var endForTodayStart = now.Date.AddDays(1).Add(endTime);
        if (now >= start.AddMinutes(-30)) return new(start, start.AddMinutes(-30), endForTodayStart, endForTodayStart.AddMinutes(30));
        var yesterdayStart = start.AddDays(-1);
        var yesterdayEnd = now.Date.Add(endTime);
        if (now < yesterdayEnd.AddMinutes(30)) return new(yesterdayStart, yesterdayStart.AddMinutes(-30), yesterdayEnd, yesterdayEnd.AddMinutes(30));
        return new(start, start.AddMinutes(-30), endForTodayStart, endForTodayStart.AddMinutes(30));
    }

    private static string BuildOutsideShiftMessage(ShiftWindow window, bool afterGrace) => afterGrace
        ? "تم انتهاء موعد دوامك، لا يمكنك الدخول الآن. سيتم السماح بالدخول مرة أخرى قبل بداية دوامك بنصف ساعة."
        : $"لا يمكنك الدخول الآن. مسموح بالدخول قبل بداية دوامك بنصف ساعة، بداية من الساعة {window.AccessStart:HH:mm}";

    private static DateTime CairoNow()
    {
        foreach (var id in new[] { "Africa/Cairo", "Egypt Standard Time" })
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(id)); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return DateTime.Now;
    }

    private sealed record ShiftWindow(DateTime Start, DateTime AccessStart, DateTime End, DateTime EndWithGrace);
}
