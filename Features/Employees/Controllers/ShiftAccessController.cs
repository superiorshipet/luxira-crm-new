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
[Route("api/v1/employees/shift-access")]
[Route("ShiftAccess")]
public class ShiftAccessController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ShiftAccessController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("CheckCurrentShift")]
    [HttpGet("/ShiftAccess/CheckCurrentShift")]
    public async Task<IActionResult> CheckCurrentShift(CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId, ct);
        if (employee == null)
        {
            return Ok(new { hasAccess = true, isShiftActive = true, message = "Non-employee or admin access." });
        }

        var now = IstanbulTimeHelper.Now;
        var currentTime = now.TimeOfDay;
        var dayOfWeek = (int)now.DayOfWeek;

        var shifts = await _context.EmployeeWorkShifts
            .Where(s => s.EmployeeId == employee.Id && s.DayOfWeek == dayOfWeek)
            .ToListAsync(ct);

        if (shifts.Count == 0)
        {
            // No strict shift defined -> unrestricted access
            return Ok(new { hasAccess = true, isShiftActive = true, shift = "Open" });
        }

        var activeShift = shifts.FirstOrDefault(s => currentTime >= s.StartTime && currentTime <= s.EndTime);
        var isShiftActive = activeShift != null;

        return Ok(new
        {
            hasAccess = isShiftActive,
            isShiftActive,
            currentTime = currentTime.ToString(@"hh\:mm"),
            activeShift = activeShift?.ShiftName
        });
    }
}
