using System.Text;
using Luxira.Api.Data;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.Communication.Hubs;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communication/conference")]
[Route("Conference")]
public sealed class ConferenceController(ApplicationDbContext context, IConfiguration configuration, S3StorageService storage) : ControllerBase
{
    [HttpGet("GetMeetings")]
    public async Task<IActionResult> GetMeetings(CancellationToken ct) => Ok(await context.ConferenceMeetings.AsNoTracking().OrderBy(item => item.ScheduledStartTime).ToListAsync(ct));

    [AllowAnonymous]
    [HttpGet("GetIceServers")]
    [HttpGet("/Conference/GetIceServers")]
    public IActionResult GetIceServers()
    {
        var servers = new List<object> { new { urls = new[] { "stun:stun.l.google.com:19302", "stun:stun1.l.google.com:19302", "stun:stun2.l.google.com:19302" } } };
        var url = configuration["TurnServer:Url"]; if (!string.IsNullOrWhiteSpace(url)) servers.Insert(0, new { urls = new[] { url }, username = configuration["TurnServer:Username"] ?? "", credential = configuration["TurnServer:Credential"] ?? configuration["TurnServer:Password"] ?? "" });
        return Ok(new { iceServers = servers });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("Dashboard")]
    [HttpGet("/Conference/Dashboard")]
    [HttpPost("/Conference/Dashboard")]
    public IActionResult Dashboard() => Ok(new { success = true });

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("DashboardData")]
    [HttpGet("/Conference/DashboardData")]
    public async Task<IActionResult> DashboardData(string? employeeId, string? callType, DateTime? date, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 5, 100); var query = context.CallRecordings.AsNoTracking(); if (!string.IsNullOrWhiteSpace(employeeId)) query = query.Where(item => item.EmployeeId == employeeId); if (!string.IsNullOrWhiteSpace(callType)) query = query.Where(item => item.CallType == callType); if (date.HasValue) { var from = date.Value.Date; var to = from.AddDays(1); query = query.Where(item => item.StartedAt >= from && item.StartedAt < to); }
        var total = await query.CountAsync(ct); var rows = await query.OrderByDescending(item => item.StartedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); var userIds = rows.Where(item => item.EmployeeId != null).Select(item => item.EmployeeId!).Distinct().ToArray(); var profiles = await context.Employees.AsNoTracking().Where(item => item.ApplicationUserId != null && userIds.Contains(item.ApplicationUserId)).GroupBy(item => item.ApplicationUserId!).Select(group => group.OrderByDescending(item => item.IsActive).First()).ToDictionaryAsync(item => item.ApplicationUserId!, ct);
        var records = rows.Select(item => { profiles.TryGetValue(item.EmployeeId ?? "", out var profile); return new { item.Id, item.EmployeeId, employee = profile?.DisplayName ?? profile?.Name ?? "موظف غير معروف", avatar = profile?.ImageUrl ?? $"/Conference/Avatar?id={Uri.EscapeDataString(item.EmployeeId ?? "")}", role = profile?.JobTitle ?? "موظف", otherParty = new { type = item.OtherPartyType ?? "client", name = item.OtherPartyName ?? "طرف غير معروف", phone = item.OtherPartyPhone ?? "-" }, department = item.Department ?? profile?.JobTitle ?? "موظف", callType = item.CallType ?? "outgoing", date = item.StartedAt.ToString("yyyy/MM/dd"), time = item.StartedAt.ToString("HH:mm"), duration = item.EndedAt.HasValue ? FormatDuration(item.EndedAt.Value - item.StartedAt) : "مستمر", size = FormatBytes(item.FileSizeBytes), audio = $"/Conference/RecordingFile?id={item.Id}", item.RecordingPath }; });
        var durationSeconds = await query.Where(item => item.EndedAt.HasValue).Select(item => new { item.StartedAt, item.EndedAt }).ToListAsync(ct); return Ok(new { records, employees = await EmployeeList(ct), pagination = new { page, pageSize, totalRecords = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) }, stats = new { totalRecords = total, totalDuration = FormatDuration(TimeSpan.FromSeconds(durationSeconds.Sum(item => Math.Max(0, (item.EndedAt!.Value - item.StartedAt).TotalSeconds)))), storageUsed = FormatBytes(await query.SumAsync(item => item.FileSizeBytes, ct)) } });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("SaveRecording")]
    [HttpPost("/Conference/SaveRecording")]
    [RequestSizeLimit(500L * 1024 * 1024)]
    public async Task<IActionResult> SaveRecording(IFormFile recordingFile, [FromForm] string? employeeId, [FromForm] string? otherPartyName, [FromForm] string? otherPartyPhone, [FromForm] string? otherPartyType, [FromForm] string? callType, [FromForm] string? department, [FromForm] DateTime? startedAt, [FromForm] DateTime? endedAt, CancellationToken ct)
    {
        if (recordingFile is null || recordingFile.Length == 0) return BadRequest(new { message = "ملف التسجيل مطلوب." }); var userId = User.GetUserId(); if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(); var stored = await storage.UploadAsync(recordingFile, "callrecordings", userId, ct); var item = new CallRecording { EmployeeId = userId, OtherPartyName = Clean(otherPartyName), OtherPartyPhone = Clean(otherPartyPhone), OtherPartyType = Clean(otherPartyType) ?? "client", CallType = Clean(callType) ?? "outgoing", Department = Clean(department), StartedAt = startedAt?.ToUniversalTime() ?? DateTime.UtcNow, EndedAt = endedAt?.ToUniversalTime(), RecordingPath = stored.PublicUrl ?? $"/api/v1/media/{Uri.EscapeDataString(stored.Key)}", RecordingS3Key = stored.Key, FileSizeBytes = recordingFile.Length, CreatedAt = IstanbulTimeHelper.Now }; context.CallRecordings.Add(item); await context.SaveChangesAsync(ct); return Ok(new { item.Id, item.RecordingPath });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("RecordingFile")]
    [HttpGet("/Conference/RecordingFile")]
    public async Task<IActionResult> RecordingFile(int id, CancellationToken ct) { var item = await context.CallRecordings.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id, ct); if (item is null) return NotFound(); if (!string.IsNullOrWhiteSpace(item.RecordingS3Key)) { Response.Headers.CacheControl = "no-store"; return Redirect(storage.GetPresignedUrl(item.RecordingS3Key, 60)); } return NotFound(); }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("DeleteRecordings")]
    [HttpPost("/Conference/DeleteRecordings")]
    public async Task<IActionResult> DeleteRecordings([FromBody] DeleteConferenceRecordingsRequest request, CancellationToken ct) { var ids = request.Ids.Where(id => id > 0).Distinct().ToArray(); if (ids.Length == 0) return BadRequest(); var rows = await context.CallRecordings.Where(item => ids.Contains(item.Id)).ToListAsync(ct); foreach (var item in rows) if (!string.IsNullOrWhiteSpace(item.RecordingS3Key)) try { await storage.DeleteAsync(item.RecordingS3Key, ct); } catch { } context.CallRecordings.RemoveRange(rows); await context.SaveChangesAsync(ct); return Ok(new { deleted = rows.Count }); }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/Conference/Index")]
    [HttpPost("/Conference/Index")]
    public IActionResult Index() => Ok(new { openCallRoom = true });

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("Employees")]
    [HttpGet("/Conference/Employees")]
    public async Task<IActionResult> Employees(CancellationToken ct) => Ok(new { employees = await EmployeeList(ct) });

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("CallProfiles")]
    [HttpGet("/Conference/CallProfiles")]
    public Task<IActionResult> CallProfiles(CancellationToken ct) => Employees(ct);

    [HttpGet("UserProfile")]
    [HttpGet("/Conference/UserProfile")]
    public async Task<IActionResult> UserProfile(string id, CancellationToken ct) { var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct); if (user is null) return NotFound(); var profile = await context.Employees.AsNoTracking().Where(item => item.ApplicationUserId == id).OrderByDescending(item => item.IsActive).FirstOrDefaultAsync(ct); return Ok(new { user.Id, name = profile?.DisplayName ?? profile?.Name ?? user.Name ?? user.UserName ?? user.Email, avatar = profile?.ImageUrl ?? $"/Conference/Avatar?id={Uri.EscapeDataString(id)}", role = profile?.JobTitle ?? "مستخدم", phone = profile?.PhoneNumber ?? user.PhoneNumber ?? "-" }); }

    [HttpGet("Avatar")]
    [HttpGet("/Conference/Avatar")]
    public async Task<IActionResult> Avatar(string id, CancellationToken ct) { var name = await context.Users.AsNoTracking().Where(item => item.Id == id).Select(item => item.Name ?? item.UserName ?? item.Email).FirstOrDefaultAsync(ct) ?? "L"; var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => char.ToUpperInvariant(part[0]))); if (initials.Length == 0) initials = "L"; var hash = Math.Abs(StringComparer.Ordinal.GetHashCode(id ?? name)); var colors = new[] { "#2563eb", "#7c3aed", "#059669", "#dc2626", "#d97706" }; var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128'><rect width='128' height='128' rx='64' fill='{colors[hash % colors.Length]}'/><text x='50%' y='54%' text-anchor='middle' dominant-baseline='middle' font-family='Arial' font-size='42' font-weight='700' fill='white'>{System.Net.WebUtility.HtmlEncode(initials)}</text></svg>"; return File(Encoding.UTF8.GetBytes(svg), "image/svg+xml"); }

    private async Task<List<object>> EmployeeList(CancellationToken ct)
    {
        var current = User.GetUserId(); var profiles = await context.Employees.AsNoTracking().Where(item => item.ApplicationUserId != null && item.ApplicationUserId != current && item.IsActive).OrderBy(item => item.Name).ToListAsync(ct); return profiles.Select(item => (object)new { id = item.ApplicationUserId, name = item.DisplayName ?? item.Name ?? "موظف", avatar = item.ImageUrl ?? $"/Conference/Avatar?id={Uri.EscapeDataString(item.ApplicationUserId!)}", role = item.JobTitle ?? "موظف", department = item.JobTitle ?? "موظف", phone = item.PhoneNumber ?? "-", isOnline = ConferenceHub.IsUserConnected(item.ApplicationUserId!), isActive = item.IsActive }).ToList();
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FormatDuration(TimeSpan value) => value.TotalHours >= 1 ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" : $"{value.Minutes:00}:{value.Seconds:00}";
    private static string FormatBytes(long value) { if (value <= 0) return "0 MB"; string[] sizes = ["B", "KB", "MB", "GB", "TB"]; var amount = (double)value; var index = 0; while (amount >= 1024 && index < sizes.Length - 1) { amount /= 1024; index++; } return $"{amount:0.##} {sizes[index]}"; }
}

public sealed record DeleteConferenceRecordingsRequest(int[] Ids);
