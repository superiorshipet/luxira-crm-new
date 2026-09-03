using System.Net;
using System.Text.RegularExpressions;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/notes")]
[Route("PersonalNotes")]
public class PersonalNotesController : ControllerBase
{
    private const int MaxHtmlLength = 60_000;
    private const int MaxPlainTextLength = 30_000;
    private readonly ApplicationDbContext _context;

    public PersonalNotesController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    [HttpGet("GetNotes")]
    public async Task<IActionResult> GetNotes(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var notes = await _context.PersonalNotes.AsNoTracking()
            .Where(note => note.ApplicationUserId == userId && !note.IsDeleted)
            .OrderByDescending(note => note.UpdatedAt ?? note.CreatedAt)
            .ToListAsync(ct);
        return Ok(notes);
    }

    [HttpGet("Mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var note = await _context.PersonalNotes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ApplicationUserId == userId, ct);
        return note is null
            ? Ok(new { success = true, exists = false, html = string.Empty, plainText = string.Empty, isDeleted = false, updatedAt = (DateTime?)null })
            : Ok(new { success = true, exists = true, id = note.Id, html = note.HtmlContent, plainText = note.PlainText, isDeleted = note.IsDeleted, createdAt = note.CreatedAt, updatedAt = note.UpdatedAt, deletedAt = note.DeletedAt });
    }

    [HttpPost]
    [HttpPost("SaveNote")]
    public Task<IActionResult> SaveNote([FromBody] SavePersonalNoteRequest request, CancellationToken ct) =>
        SaveCoreAsync(request.HtmlContent, request.PlainText, ct);

    [HttpPost("Save")]
    public Task<IActionResult> Save([FromForm] string? html, [FromForm] string? plainText, CancellationToken ct) =>
        SaveCoreAsync(html, plainText, ct);

    [HttpPost("Delete")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var note = await _context.PersonalNotes.SingleOrDefaultAsync(item => item.ApplicationUserId == userId, ct);
        if (note is null || note.IsDeleted)
        {
            await transaction.CommitAsync(ct);
            return Ok(new { success = true, alreadyDeleted = true });
        }

        var now = DateTime.UtcNow;
        var changedByName = await GetDisplayNameAsync(userId, ct);
        _context.PersonalNoteHistories.Add(NewHistory(note, "Delete", note.HtmlContent, string.Empty,
            note.PlainText, string.Empty, now, userId, changedByName));
        note.HtmlContent = string.Empty;
        note.PlainText = string.Empty;
        note.IsDeleted = true;
        note.UpdatedAt = now;
        note.DeletedAt = now;
        note.DeletedByUserId = userId;
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true, deletedAt = now });
    }

    [HttpGet("History")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> History([FromQuery] string? userId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(currentUserId)) return Unauthorized();
        var targetUserId = string.IsNullOrWhiteSpace(userId) ? currentUserId : userId.Trim();
        var employeeName = await GetDisplayNameAsync(targetUserId, ct);
        var items = await _context.PersonalNoteHistories.AsNoTracking()
            .Where(item => item.ApplicationUserId == targetUserId)
            .OrderByDescending(item => item.Id)
            .Select(item => new
            {
                id = item.Id,
                action = item.Action,
                previousHtml = item.PreviousHtmlContent,
                newHtml = item.NewHtmlContent,
                previousPlainText = item.PreviousPlainText,
                newPlainText = item.NewPlainText,
                changedAt = item.ChangedAt,
                changedByUserId = item.ChangedByUserId,
                changedByName = item.ChangedByName
            }).ToListAsync(ct);
        return Ok(new { success = true, userId = targetUserId, employeeName, items });
    }

    [HttpGet("AdminOverview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminOverview(CancellationToken ct)
    {
        var adminUserId = User.GetUserId() ?? string.Empty;
        var allowedRoles = new[] { "CallCenter", "FollowUpDepartment", "TeamLeader", "Team Leader" };
        var items = await (
            from note in _context.PersonalNotes.AsNoTracking()
            join employee in _context.Employees.AsNoTracking() on note.ApplicationUserId equals employee.ApplicationUserId into employees
            from employee in employees.DefaultIfEmpty()
            join user in _context.Users.AsNoTracking() on note.ApplicationUserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            where note.ApplicationUserId != adminUserId
                && _context.UserRoles.Any(userRole => userRole.UserId == note.ApplicationUserId
                    && userRole.Role != null && allowedRoles.Contains(userRole.Role.Name!))
            orderby employee != null && employee.DisplayName != null && employee.DisplayName != "" ? employee.DisplayName
                : employee != null && employee.Name != null && employee.Name != "" ? employee.Name
                : user.Name ?? user.Email ?? "موظف"
            select new
            {
                userId = note.ApplicationUserId,
                employeeName = employee != null && employee.DisplayName != null && employee.DisplayName != "" ? employee.DisplayName
                    : employee != null && employee.Name != null && employee.Name != "" ? employee.Name
                    : user.Name ?? user.Email ?? "موظف",
                imageUrl = employee != null && employee.ImageUrl != null && employee.ImageUrl != "" ? employee.ImageUrl : "/static/DefaultImage.svg",
                html = note.HtmlContent,
                plainText = note.PlainText,
                isDeleted = note.IsDeleted,
                updatedAt = note.UpdatedAt,
                deletedAt = note.DeletedAt,
                historyCount = _context.PersonalNoteHistories.LongCount(history => history.ApplicationUserId == note.ApplicationUserId)
            }).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    private async Task<IActionResult> SaveCoreAsync(string? html, string? plainText, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var cleanHtml = SanitizeNoteHtml(html ?? string.Empty);
        var cleanPlainText = NormalizePlainText(plainText ?? string.Empty);
        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var note = await _context.PersonalNotes.SingleOrDefaultAsync(item => item.ApplicationUserId == userId, ct);
        if (note is not null && !note.IsDeleted
            && string.Equals(note.HtmlContent, cleanHtml, StringComparison.Ordinal)
            && string.Equals(note.PlainText, cleanPlainText, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(ct);
            return Ok(new { success = true, unchanged = true, id = note.Id, html = cleanHtml, plainText = cleanPlainText, updatedAt = note.UpdatedAt ?? now });
        }

        var action = note is null ? "Create" : note.IsDeleted ? "Restore" : "Edit";
        var previousHtml = note?.HtmlContent ?? string.Empty;
        var previousPlainText = note?.PlainText ?? string.Empty;
        if (note is null)
        {
            note = new PersonalNote { ApplicationUserId = userId, CreatedAt = now };
            _context.PersonalNotes.Add(note);
        }
        note.HtmlContent = cleanHtml;
        note.PlainText = cleanPlainText;
        note.IsDeleted = false;
        note.UpdatedAt = now;
        note.DeletedAt = null;
        note.DeletedByUserId = null;

        var changedByName = await GetDisplayNameAsync(userId, ct);
        await _context.SaveChangesAsync(ct);
        _context.PersonalNoteHistories.Add(NewHistory(note, action, previousHtml, cleanHtml,
            previousPlainText, cleanPlainText, now, userId, changedByName));
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true, id = note.Id, html = cleanHtml, plainText = cleanPlainText, action, updatedAt = now });
    }

    private static PersonalNoteHistory NewHistory(PersonalNote note, string action, string previousHtml,
        string newHtml, string previousPlainText, string newPlainText, DateTime changedAt,
        string changedByUserId, string changedByName) => new()
    {
        PersonalNoteId = note.Id,
        PersonalNote = note,
        ApplicationUserId = note.ApplicationUserId,
        Action = action,
        PreviousHtmlContent = previousHtml,
        NewHtmlContent = newHtml,
        PreviousPlainText = previousPlainText,
        NewPlainText = newPlainText,
        ChangedAt = changedAt,
        ChangedByUserId = changedByUserId,
        ChangedByName = changedByName
    };

    private async Task<string> GetDisplayNameAsync(string userId, CancellationToken ct)
    {
        var employeeName = await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => employee.DisplayName ?? employee.Name)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(employeeName)) return employeeName.Trim();
        var userName = await _context.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => user.Name ?? user.Email ?? user.UserName).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(userName) ? "مستخدم" : userName.Trim();
    }

    private static string NormalizePlainText(string value)
    {
        value = value.Replace("\0", string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        return value.Length <= MaxPlainTextLength ? value : value[..MaxPlainTextLength];
    }

    private static string SanitizeNoteHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Replace("\0", string.Empty);
        if (value.Length > MaxHtmlLength * 2) value = value[..(MaxHtmlLength * 2)];
        value = Regex.Replace(value, @"<\s*(script|style|iframe|object|embed|form|input|button|link|meta)\b[^>]*>.*?<\s*/\s*\1\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, @"<\s*(script|style|iframe|object|embed|form|input|button|link|meta)\b[^>]*/?\s*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, @"</?(?!div\b|p\b|br\b|span\b|strong\b|b\b|em\b|i\b|u\b|ul\b|ol\b|li\b)[a-zA-Z][^>]*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, @"<(?<tag>div|p|span|strong|b|em|i|u|ul|ol|li)\b(?<attrs>[^>]*)>", match =>
        {
            var tag = match.Groups["tag"].Value.ToLowerInvariant();
            var style = ExtractSafeStyle(match.Groups["attrs"].Value);
            return string.IsNullOrWhiteSpace(style) ? $"<{tag}>" : $"<{tag} style=\"{WebUtility.HtmlEncode(style)}\">";
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, @"<br\b[^>]*>", "<br>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (value.Length > MaxHtmlLength) value = value[..MaxHtmlLength];
        return value.Trim();
    }

    private static string ExtractSafeStyle(string attributes)
    {
        var declarations = new List<string>();
        var align = Regex.Match(attributes, @"\balign\s*=\s*[""']?(?<value>left|right|center)[""']?", RegexOptions.IgnoreCase);
        if (align.Success) declarations.Add("text-align:" + align.Groups["value"].Value.ToLowerInvariant());
        var style = Regex.Match(attributes, @"\bstyle\s*=\s*([""'])(?<value>.*?)\1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!style.Success) return string.Join(";", declarations.Distinct(StringComparer.OrdinalIgnoreCase));

        foreach (var declaration in style.Groups["value"].Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = declaration.Split(':', 2);
            if (parts.Length != 2) continue;
            var property = parts[0].Trim().ToLowerInvariant();
            var styleValue = parts[1].Trim();
            if (styleValue.Length == 0 || styleValue.Contains("url(", StringComparison.OrdinalIgnoreCase)
                || styleValue.Contains("expression", StringComparison.OrdinalIgnoreCase)
                || styleValue.Contains("javascript", StringComparison.OrdinalIgnoreCase)) continue;

            if (property == "color" && Regex.IsMatch(styleValue, @"^(#[0-9a-fA-F]{3,8}|rgb[a]?\([0-9,\.\s%]+\)|[a-zA-Z]{3,20})$")) declarations.Add("color:" + styleValue);
            else if (property == "text-align" && new[] { "left", "right", "center" }.Contains(styleValue, StringComparer.OrdinalIgnoreCase)) declarations.Add("text-align:" + styleValue.ToLowerInvariant());
            else if (property == "font-weight" && (styleValue.Equals("bold", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(styleValue, "^[1-9]00$"))) declarations.Add("font-weight:" + styleValue);
            else if (property == "font-size" && Regex.Match(styleValue, @"^(?<size>\d{1,2})px$", RegexOptions.IgnoreCase) is { Success: true } sizeMatch && int.TryParse(sizeMatch.Groups["size"].Value, out var size) && size is >= 8 and <= 72) declarations.Add($"font-size:{size}px");
            else if (property == "text-decoration" && styleValue.Contains("underline", StringComparison.OrdinalIgnoreCase)) declarations.Add("text-decoration:underline");
            else if (property == "direction" && new[] { "rtl", "ltr" }.Contains(styleValue, StringComparer.OrdinalIgnoreCase)) declarations.Add("direction:" + styleValue.ToLowerInvariant());
        }
        return string.Join(";", declarations.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}

public sealed record SavePersonalNoteRequest(string HtmlContent, string PlainText);
