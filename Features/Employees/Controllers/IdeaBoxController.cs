using System.Globalization;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("IdeaBox")]
[Route("api/v1/employees/ideas")]
public sealed class IdeaBoxController : ControllerBase
{
    private const int MaxIdeaLength = 2_000;
    private readonly ApplicationDbContext _context;

    public IdeaBoxController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    [HttpGet("Index")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var rows = await _context.IdeaSuggestions.AsNoTracking()
            .OrderByDescending(idea => idea.CreatedAtUtc).ThenByDescending(idea => idea.Id)
            .Take(1_000)
            .ToListAsync(ct);
        var items = rows.Select(idea => new
            {
                idea.Id,
                idea.UserId,
                idea.EmployeeName,
                idea.EmployeeImage,
                idea.IdeaText,
                idea.CreatedAtUtc,
                createdAtText = FormatApplicationTime(idea.CreatedAtUtc),
                isPendingAdminNotification = idea.AdminAcknowledgedAtUtc == null
            });
        return Ok(new { items });
    }

    [HttpPost("Submit")]
    public async Task<IActionResult> Submit([FromForm] string? ideaText, CancellationToken ct)
    {
        var text = (ideaText ?? string.Empty).Trim();
        if (text.Length < 5)
            return BadRequest(new { success = false, message = "اكتب الفكرة بشكل أوضح قبل الإرسال." });
        if (text.Length > MaxIdeaLength)
            return BadRequest(new { success = false, message = $"الحد الأقصى للفكرة هو {MaxIdeaLength} حرف." });

        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { success = false, message = "تعذر تحديد حساب الموظف الحالي." });

        var identity = await (
            from user in _context.Users.AsNoTracking()
            join employee in _context.Employees.AsNoTracking() on user.Id equals employee.ApplicationUserId into employees
            from employee in employees.DefaultIfEmpty()
            where user.Id == userId
            select new
            {
                Name = employee != null && employee.DisplayName != null && employee.DisplayName != "" ? employee.DisplayName
                    : employee != null && employee.Name != "" ? employee.Name
                    : user.Name ?? user.Email ?? "موظف",
                Image = employee != null ? employee.ImageUrl : null
            }).SingleOrDefaultAsync(ct);

        var employeeName = identity?.Name ?? User.Identity?.Name ?? "موظف";
        if (employeeName.Length > 255) employeeName = employeeName[..255];
        var idea = new IdeaSuggestion
        {
            UserId = userId,
            EmployeeName = employeeName,
            EmployeeImage = NormalizeImageUrl(identity?.Image),
            IdeaText = text,
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.IdeaSuggestions.Add(idea);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id = idea.Id, message = "تم إرسال الفكرة للإدارة بنجاح." });
    }

    [HttpGet("GetPendingAdminNotifications")]
    [Authorize(Roles = "Admin")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetPendingAdminNotifications(CancellationToken ct)
    {
        var rows = await _context.IdeaSuggestions.AsNoTracking()
            .Where(idea => idea.AdminAcknowledgedAtUtc == null)
            .OrderBy(idea => idea.CreatedAtUtc).ThenBy(idea => idea.Id)
            .Take(8)
            .ToListAsync(ct);
        var items = rows.Select(idea => new
            {
                id = idea.Id,
                employeeName = idea.EmployeeName,
                ideaText = idea.IdeaText,
                createdAtText = FormatApplicationTime(idea.CreatedAtUtc)
            });
        return Ok(new { success = true, items });
    }

    [HttpPost("MarkAdminNotificationRead")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MarkAdminNotificationRead([FromForm] long id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new { success = false });
        await _context.IdeaSuggestions.Where(idea => idea.Id == id && idea.AdminAcknowledgedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(idea => idea.AdminAcknowledgedAtUtc, DateTime.UtcNow), ct);
        return Ok(new { success = true });
    }

    private static string? NormalizeImageUrl(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > 1_000) return null;
        return text.StartsWith('/') || Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? text
                : "/" + text.TrimStart('/');
    }

    private static string FormatApplicationTime(DateTime utcValue)
    {
        var utc = DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");
            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        catch (TimeZoneNotFoundException)
        {
            return utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
