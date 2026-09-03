using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Luxira.Api.Infrastructure.S3;
using System.Text.Json;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/errors")]
[Route("EmployeeErrors")]
[Route("Violations")]
public class EmployeeErrorsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;

    public EmployeeErrorsController(ApplicationDbContext context, S3StorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    [HttpGet("GetErrors")]
    public async Task<ActionResult<List<EmployeeViolationDto>>> GetErrors([FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = _context.EmployeeViolations
            .Include(v => v.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(v => v.EmployeeId == employeeId.Value);
        }

        var list = await query.OrderByDescending(v => v.OccurredAt)
            .Select(v => new EmployeeViolationDto(v.Id, v.EmployeeId, v.Employee != null ? v.Employee.Name : null, v.Title, v.Description, v.PenaltyAmount, v.OccurredAt, v.IssuedByUserId))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [HttpPost("ReportError")]
    public async Task<ActionResult<EmployeeViolationDto>> ReportError([FromBody] ReportViolationRequest request, CancellationToken ct)
    {
        var v = new EmployeeViolation
        {
            EmployeeId = request.EmployeeId,
            Title = request.Title,
            Description = request.Description,
            PenaltyAmount = request.PenaltyAmount,
            OccurredAt = DateTime.UtcNow,
            IssuedByUserId = User.GetUserId() ?? "system"
        };

        await _context.EmployeeViolations.AddAsync(v, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new EmployeeViolationDto(v.Id, v.EmployeeId, null, v.Title, v.Description, v.PenaltyAmount, v.OccurredAt, v.IssuedByUserId));
    }

    [HttpGet("/EmployeeErrors/Stores")]
    public async Task<IActionResult> Stores(CancellationToken ct) => Ok(new
    {
        success = true,
        items = await _context.ManufacturingCompanies.AsNoTracking().Where(store => store.IsShown)
            .OrderBy(store => store.Name).Select(store => new { id = store.Id, name = store.Name }).ToListAsync(ct)
    });

    [HttpGet("/EmployeeErrors/EmployeesByStore")]
    public async Task<IActionResult> EmployeesByStore([FromQuery] int storeId, CancellationToken ct)
    {
        var userIds = _context.EmployeeManufacturingCompanies.AsNoTracking()
            .Where(access => access.ManufacturingCompanyId == storeId && access.CanSeeManufacturingCompany)
            .Select(access => access.ApplicationUserId);
        var items = await _context.Employees.AsNoTracking()
            .Where(employee => !employee.IsDeleted && employee.IsActive && employee.ApplicationUserId != null && userIds.Contains(employee.ApplicationUserId))
            .OrderBy(employee => employee.DisplayName ?? employee.Name)
            .Select(employee => new { id = employee.Id, name = employee.DisplayName ?? employee.Name, employee.ImageUrl }).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/EmployeeErrors/ActiveEmployees")]
    public async Task<IActionResult> ActiveEmployees(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var items = await _context.Employees.AsNoTracking().Where(employee => !employee.IsDeleted && employee.IsActive && employee.ApplicationUserId == userId)
            .Select(employee => new { id = employee.Id, name = employee.DisplayName ?? employee.Name }).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/EmployeeErrors/Panel")]
    public async Task<IActionResult> Panel(CancellationToken ct)
    {
        var query = VisibleErrors().Where(error => !error.IsDeleted);
        var take = CanManageAll() ? 5_000 : 200;
        return Ok(new { success = true, items = await query.OrderByDescending(error => error.CreatedAt).Take(take).ToListAsync(ct) });
    }

    [HttpGet("/EmployeeErrors/Get")]
    public async Task<IActionResult> Get([FromQuery] int id, CancellationToken ct)
    {
        var item = await VisibleErrors().FirstOrDefaultAsync(error => error.Id == id && !error.IsDeleted, ct);
        return item is null ? NotFound(new { success = false, message = "لم يتم العثور على الخطأ" }) : Ok(new { success = true, item });
    }

    [HttpPost("/EmployeeErrors/Create")]
    [RequestSizeLimit(35 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        [FromForm] int? storeId,
        [FromForm] int? employeeId,
        [FromForm] string? pageUrl,
        [FromForm] string? errorReason,
        [FromForm] string? otherReason,
        [FromForm] string? errorText,
        [FromForm] List<IFormFile>? imageFiles,
        [FromForm] IFormFile? imageFile,
        CancellationToken ct)
    {
        if (employeeId is not > 0) return BadRequest(new { success = false, message = "اختار الموظف." });
        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employeeId && !item.IsDeleted, ct);
        if (employee is null) return NotFound(new { success = false, message = "الموظف غير موجود." });
        if (storeId is > 0 && !await _context.EmployeeManufacturingCompanies.AsNoTracking().AnyAsync(access => access.ManufacturingCompanyId == storeId && access.ApplicationUserId == employee.ApplicationUserId && access.CanSeeManufacturingCompany, ct))
            return BadRequest(new { success = false, message = "الموظف غير مرتبط بالمتجر." });
        var text = ResolveErrorText(errorReason, otherReason, errorText);
        if (string.IsNullOrWhiteSpace(text)) return BadRequest(new { success = false, message = "سبب الخطأ مطلوب." });
        var files = CollectImages(imageFiles, imageFile);
        if (files.Count == 0) return BadRequest(new { success = false, message = "صورة الخطأ مطلوبة." });
        var urls = await UploadImages(files, ct);
        var count = await _context.EmployeeErrors.AsNoTracking().CountAsync(error => error.EmployeeId == employee.Id && error.ErrorText == text, ct) + 1;
        var entity = new EmployeeError
        {
            EmployeeId = employee.Id,
            EmployeeNameSnapshot = employee.DisplayName ?? employee.Name,
            PageUrl = pageUrl?.Trim(),
            ChatType = "Meta",
            ErrorText = text,
            ImageUrl = JsonSerializer.Serialize(urls),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.GetUserId(),
            CreatedByUserName = User.Identity?.Name,
            ErrorCount = count,
            SeverityLevel = 1
        };
        _context.EmployeeErrors.Add(entity);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, item = entity });
    }

    [HttpPost("/EmployeeErrors/Update")]
    public async Task<IActionResult> Update(
        [FromForm] int id,
        [FromForm] string? pageUrl,
        [FromForm] string? errorReason,
        [FromForm] string? otherReason,
        [FromForm] string? errorText,
        [FromForm] string? existingImageUrls,
        [FromForm] List<IFormFile>? imageFiles,
        [FromForm] IFormFile? imageFile,
        CancellationToken ct)
    {
        var item = await _context.EmployeeErrors.FirstOrDefaultAsync(error => error.Id == id && !error.IsDeleted, ct);
        if (item is null || !CanEdit(item)) return NotFound(new { success = false, message = "ليس لديك صلاحية تعديل هذا الخطأ" });
        var text = ResolveErrorText(errorReason, otherReason, errorText);
        if (string.IsNullOrWhiteSpace(text)) return BadRequest(new { success = false, message = "سبب الخطأ مطلوب." });
        var old = new { item.PageUrl, item.ChatType, item.ErrorText, item.ImageUrl };
        var urls = ParseUrls(existingImageUrls);
        urls.AddRange(await UploadImages(CollectImages(imageFiles, imageFile), ct));
        item.PageUrl = pageUrl?.Trim();
        item.ErrorText = text;
        item.ImageUrl = JsonSerializer.Serialize(urls.Distinct().Take(10));
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedByUserId = User.GetUserId();
        item.UpdatedByName = User.Identity?.Name;
        _context.EmployeeErrorEditHistories.Add(new EmployeeErrorEditHistory
        {
            EmployeeErrorId = item.Id, EmployeeId = item.EmployeeId, EmployeeNameSnapshot = item.EmployeeNameSnapshot,
            OldPageUrl = old.PageUrl, NewPageUrl = item.PageUrl, OldChatType = old.ChatType, NewChatType = item.ChatType,
            OldErrorText = old.ErrorText, NewErrorText = item.ErrorText, OldImageUrl = old.ImageUrl, NewImageUrl = item.ImageUrl,
            EditedAt = DateTime.UtcNow, EditedByUserId = User.GetUserId(), EditedByName = User.Identity?.Name
        });
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, item });
    }

    [HttpPost("/EmployeeErrors/Delete")]
    public async Task<IActionResult> Delete([FromForm] int id, CancellationToken ct)
    {
        var item = await _context.EmployeeErrors.FirstOrDefaultAsync(error => error.Id == id && !error.IsDeleted, ct);
        if (item is null || !CanEdit(item)) return NotFound(new { success = false, message = "ليس لديك صلاحية حذف هذا الخطأ" });
        SoftDelete(item);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [AllowAnonymous]
    [HttpGet("/EmployeeErrors/GetPendingError")]
    public async Task<IActionResult> GetPendingError(CancellationToken ct)
    {
        var employeeId = await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId == User.GetUserId() && !employee.IsDeleted)
            .Select(employee => (int?)employee.Id).FirstOrDefaultAsync(ct);
        if (!employeeId.HasValue) return Ok(new { success = false });
        var item = await _context.EmployeeErrors.AsNoTracking().Where(error => error.EmployeeId == employeeId && !error.IsDeleted && !error.IsAcknowledged)
            .OrderBy(error => error.ErrorCount).ThenBy(error => error.CreatedAt).FirstOrDefaultAsync(ct);
        return Ok(item is null ? new { success = false } : new { success = true, item });
    }

    [AllowAnonymous]
    [HttpPost("/EmployeeErrors/SubmitErrorReason")]
    public async Task<IActionResult> SubmitErrorReason([FromForm] int id, [FromForm] string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest(new { success = false, message = "يجب كتابة السبب" });
        var target = await CurrentEmployeeError(id, ct);
        if (target is null) return NotFound(new { success = false, message = "الخطأ غير موجود" });
        var related = await _context.EmployeeErrors.Where(error => error.EmployeeId == target.EmployeeId && error.ErrorText == target.ErrorText && error.ErrorCount <= target.ErrorCount && !error.IsAcknowledged).ToListAsync(ct);
        foreach (var error in related) { error.EmployeeReason = reason.Trim(); error.IsReasonProvided = true; }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [AllowAnonymous]
    [HttpPost("/EmployeeErrors/AcknowledgeError")]
    public async Task<IActionResult> AcknowledgeError([FromForm] int id, CancellationToken ct)
    {
        var target = await CurrentEmployeeError(id, ct);
        if (target is null || !target.IsReasonProvided) return BadRequest(new { success = false, message = "يجب كتابة السبب أولاً" });
        await _context.EmployeeErrors.Where(error => error.EmployeeId == target.EmployeeId && error.ErrorText == target.ErrorText && error.ErrorCount <= target.ErrorCount && !error.IsAcknowledged)
            .ExecuteUpdateAsync(setters => setters.SetProperty(error => error.IsAcknowledged, true), ct);
        return Ok(new { success = true });
    }

    [HttpPost("/EmployeeErrors/DeleteAll")]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        if (!CanManageAll()) return Forbid();
        var changed = await _context.EmployeeErrors.Where(error => !error.IsDeleted).ExecuteUpdateAsync(setters => setters
            .SetProperty(error => error.IsDeleted, true).SetProperty(error => error.DeletedAt, DateTime.UtcNow)
            .SetProperty(error => error.DeletedByUserId, User.GetUserId()).SetProperty(error => error.DeletedByName, User.Identity!.Name), ct);
        return Ok(new { success = true, deletedCount = changed });
    }

    [HttpGet("/EmployeeErrors/EditHistory")]
    public async Task<IActionResult> EditHistory(CancellationToken ct) => Ok(new { success = true, items = await _context.EmployeeErrorEditHistories.AsNoTracking()
        .OrderByDescending(history => history.EditedAt ?? history.CreatedAt).Take(200).ToListAsync(ct) });

    [HttpGet("/EmployeeErrors/DeletedHistory")]
    public async Task<IActionResult> DeletedHistory(CancellationToken ct) => Ok(new { success = true, items = await _context.EmployeeErrors.AsNoTracking().Include(error => error.Employee)
        .Where(error => error.IsDeleted).OrderByDescending(error => error.DeletedAt).Take(200).ToListAsync(ct) });

    [HttpPost("/EmployeeErrors/Restore")]
    public async Task<IActionResult> Restore([FromForm] int id, CancellationToken ct)
    {
        var changed = await _context.EmployeeErrors.Where(error => error.Id == id && error.IsDeleted).ExecuteUpdateAsync(setters => setters
            .SetProperty(error => error.IsDeleted, false).SetProperty(error => error.DeletedAt, (DateTime?)null)
            .SetProperty(error => error.DeletedByUserId, (string?)null).SetProperty(error => error.DeletedByName, (string?)null), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [HttpPost("/EmployeeErrors/RestoreAll")]
    public async Task<IActionResult> RestoreAll(CancellationToken ct)
    {
        var changed = await _context.EmployeeErrors.Where(error => error.IsDeleted).ExecuteUpdateAsync(setters => setters
            .SetProperty(error => error.IsDeleted, false).SetProperty(error => error.DeletedAt, (DateTime?)null)
            .SetProperty(error => error.DeletedByUserId, (string?)null).SetProperty(error => error.DeletedByName, (string?)null), ct);
        return Ok(new { success = true, restoredCount = changed });
    }

    [HttpPost("/EmployeeErrors/PermanentDelete")]
    public async Task<IActionResult> PermanentDelete([FromForm] int id, CancellationToken ct)
    {
        if (!CanManageAll()) return Forbid();
        var changed = await _context.EmployeeErrors.Where(error => error.Id == id && error.IsDeleted).ExecuteDeleteAsync(ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    private IQueryable<EmployeeError> VisibleErrors()
    {
        var query = _context.EmployeeErrors.AsNoTracking().Include(error => error.Employee).AsQueryable();
        if (!CanManageAll())
        {
            var userId = User.GetUserId();
            query = query.Where(error => error.Employee != null && error.Employee.ApplicationUserId == userId);
        }
        return query;
    }

    private bool CanManageAll() => User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector") || User.IsInRole("TeamLeader");
    private bool CanEdit(EmployeeError error) => CanManageAll() || error.CreatedByUserId == User.GetUserId();
    private async Task<EmployeeError?> CurrentEmployeeError(int id, CancellationToken ct) => await _context.EmployeeErrors.FirstOrDefaultAsync(error =>
        error.Id == id && !error.IsDeleted && _context.Employees.Any(employee => employee.Id == error.EmployeeId && employee.ApplicationUserId == User.GetUserId()), ct);
    private void SoftDelete(EmployeeError error) { error.IsDeleted = true; error.DeletedAt = DateTime.UtcNow; error.DeletedByUserId = User.GetUserId(); error.DeletedByName = User.Identity?.Name; }
    private static string ResolveErrorText(string? reason, string? other, string? text) =>
        string.Equals(reason?.Trim(), "other", StringComparison.OrdinalIgnoreCase) || reason?.Trim() == "أخرى" ? other?.Trim() ?? "" : reason?.Trim() ?? text?.Trim() ?? "";
    private static List<IFormFile> CollectImages(List<IFormFile>? files, IFormFile? file) => (files ?? []).Concat(file is null ? [] : [file])
        .Where(item => item.Length > 0 && (item.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)).Take(10).ToList();
    private async Task<List<string>> UploadImages(List<IFormFile> files, CancellationToken ct)
    {
        var urls = new List<string>();
        foreach (var file in files)
        {
            var stored = await _storage.UploadAsync(file, "employee-errors", User.GetUserId(), ct);
            if (!string.IsNullOrWhiteSpace(stored.PublicUrl)) urls.Add(stored.PublicUrl);
        }
        return urls;
    }
    private static List<string> ParseUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(value) ?? []; }
        catch (JsonException) { return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); }
    }
}

public record EmployeeViolationDto(int Id, int EmployeeId, string? EmployeeName, string Title, string Description, decimal PenaltyAmount, DateTime OccurredAt, string IssuedByUserId);
public record ReportViolationRequest(int EmployeeId, string Title, string Description, decimal PenaltyAmount);
