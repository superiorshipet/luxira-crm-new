using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Infrastructure.BackgroundServices;
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
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> UploadLocks = new();
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly S3StorageService _storage;
    private readonly ScreenRecordFinalizeSignal _finalizeSignal;

    public ScreenRecordsController(ApplicationDbContext context, IWebHostEnvironment environment, S3StorageService storage, ScreenRecordFinalizeSignal finalizeSignal)
    {
        _context = context;
        _environment = environment;
        _storage = storage;
        _finalizeSignal = finalizeSignal;
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

    [HttpPost("UploadChunk")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadChunk([FromForm] IFormFile videoChunk, [FromForm] string sessionId, [FromForm] DateTime? recordDate, CancellationToken ct)
    {
        if (videoChunk is not { Length: > 0 } || string.IsNullOrWhiteSpace(sessionId)) return BadRequest();
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var safeUser = string.Concat(userId.Where(char.IsLetterOrDigit));
        var date = (recordDate ?? DateTime.UtcNow).Date;
        var directory = Path.Combine(_environment.WebRootPath, "ScreenRecords", safeUser, date.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{date:yyyy-MM-dd}.webm");
        var gate = UploadLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
            await videoChunk.CopyToAsync(stream, ct);
        }
        finally { gate.Release(); }
        var relativePath = $"/ScreenRecords/{safeUser}/{date:yyyy-MM-dd}/{date:yyyy-MM-dd}.webm";
        var now = DateTime.UtcNow;
        var record = await _context.ScreenRecords.FirstOrDefaultAsync(item => item.EmployeeId == userId && item.Date == date, ct);
        if (record is null)
        {
            record = new ScreenRecord { EmployeeId = userId, Date = date, StartTime = now, VideoPath = relativePath, CreatedAt = now };
            _context.ScreenRecords.Add(record);
            _finalizeSignal.Request();
        }
        else record.EndTime = now;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, record.Id });
    }

    [HttpGet("Dashboard")]
    [HttpPost("Dashboard")]
    [HttpGet("Index")]
    [HttpPost("Index")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public Task<IActionResult> Dashboard([FromQuery] string? employeeId, [FromQuery] DateTime? date, CancellationToken ct) => DashboardData(employeeId, date, 1, 10, ct);

    [HttpGet("DashboardData")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> DashboardData(string? employeeId, DateTime? date, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        var query = _context.ScreenRecords.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(employeeId)) query = query.Where(item => item.EmployeeId == employeeId);
        if (date.HasValue) query = query.Where(item => item.Date == date.Value.Date);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(item => item.Date).ThenByDescending(item => item.StartTime).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { success = true, rows, total, page, pageSize });
    }

    [HttpGet("Watch/{id:int}.webm")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Watch(int id, CancellationToken ct)
    {
        var record = await _context.ScreenRecords.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (record is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(record.VideoS3Key)) return Redirect(_storage.GetPresignedUrl(record.VideoS3Key, 60));
        var root = Path.GetFullPath(_environment.WebRootPath) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, record.VideoPath.TrimStart('/')));
        if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !System.IO.File.Exists(fullPath)) return NotFound();
        return File(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), "video/webm", enableRangeProcessing: true);
    }

    [HttpPost("DeleteSelected")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public Task<IActionResult> DeleteSelected([FromForm] int[] selectedRecordIds, CancellationToken ct) => DeleteByIds(selectedRecordIds, ct);

    [HttpPost("DeleteRecords")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public Task<IActionResult> DeleteRecords([FromBody] DeleteScreenRecordsRequest request, CancellationToken ct) => DeleteByIds(request.Ids, ct);

    private async Task<IActionResult> DeleteByIds(IEnumerable<int>? values, CancellationToken ct)
    {
        var ids = (values ?? []).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0) return BadRequest(new { message = "لم يتم تحديد أي تسجيلات للحذف." });
        var records = await _context.ScreenRecords.Where(item => ids.Contains(item.Id)).ToListAsync(ct);
        foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(record.VideoS3Key)) { try { await _storage.DeleteAsync(record.VideoS3Key, ct); } catch { } }
            var root = Path.GetFullPath(_environment.WebRootPath) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, record.VideoPath.TrimStart('/')));
            if (fullPath.StartsWith(root, StringComparison.Ordinal) && System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }
        _context.ScreenRecords.RemoveRange(records);
        await _context.SaveChangesAsync(ct);
        return Ok(new { deleted = records.Count });
    }
}

public record UploadScreenRecordRequest(
    string EmployeeId,
    DateTime Date,
    DateTime StartTime,
    DateTime? EndTime,
    string VideoPath,
    string? VideoS3Key);
public sealed record DeleteScreenRecordsRequest(int[] Ids);
