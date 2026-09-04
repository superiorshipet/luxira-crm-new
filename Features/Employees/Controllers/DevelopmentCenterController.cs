using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,SoftwareDeveloper")]
[Route("api/v1/development-center")]
[Route("DevelopmentCenter")]
public sealed class DevelopmentCenterController(ApplicationDbContext context, S3StorageService storage) : ControllerBase
{
    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/DevelopmentCenter/Index")]
    [HttpPost("/DevelopmentCenter/Index")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tasks = await context.SystemDevelopmentTasks.AsNoTracking().Include(item => item.Images).AsSplitQuery().OrderByDescending(item => item.IsPinned).ThenBy(item => item.Category).ThenBy(item => item.SortOrder).ThenBy(item => item.Id).Take(1000).ToListAsync(ct);
        var ids = tasks.Select(item => item.Id).ToArray(); var assignments = await context.DevelopmentTaskAssignments.AsNoTracking().Where(item => ids.Contains(item.TaskId)).ToListAsync(ct); var comments = await context.DevelopmentTaskComments.AsNoTracking().Where(item => ids.Contains(item.TaskId)).OrderBy(item => item.CreatedAt).ToListAsync(ct);
        return Ok(new { active = tasks.Where(item => !item.IsDeleted), deleted = tasks.Where(item => item.IsDeleted), assignments, comments });
    }

    [HttpPost("Create")]
    [HttpPost("/DevelopmentCenter/Create")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] DevelopmentTaskRequest request, CancellationToken ct)
    {
        var error = Validate(request); if (error is not null) return BadRequest(new { success = false, message = error });
        var item = new SystemDevelopmentTask { Title = request.Title.Trim(), Description = Clean(request.Description), Category = NormalizeCategory(request.Category), SortOrder = await NextOrder(NormalizeCategory(request.Category), ct), EstimatedMinutes = request.EstimatedMinutes, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name };
        context.SystemDevelopmentTasks.Add(item); await context.SaveChangesAsync(ct); await SaveImages(item, request.Images, ct); await AutoAssign(item, ct); Audit(item, "Created", "إضافة مهمة جديدة", null, JsonSerializer.Serialize(item)); await context.SaveChangesAsync(ct); return Ok(new { success = true, id = item.Id, task = item });
    }

    [HttpPost("Update")]
    [HttpPost("/DevelopmentCenter/Update")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update([FromForm] DevelopmentTaskRequest request, CancellationToken ct)
    {
        var error = Validate(request); if (error is not null || request.Id <= 0) return BadRequest(new { success = false, message = error ?? "المهمة غير صحيحة." });
        var item = await context.SystemDevelopmentTasks.Include(row => row.Images).FirstOrDefaultAsync(row => row.Id == request.Id && !row.IsDeleted, ct); if (item is null) return NotFound(); var old = JsonSerializer.Serialize(item);
        var category = NormalizeCategory(request.Category); if (!item.IsCompleted && category != item.Category) { item.Category = category; item.SortOrder = await NextOrder(category, ct); }
        item.Title = request.Title.Trim(); item.Description = Clean(request.Description); item.EstimatedMinutes = request.EstimatedMinutes; Touch(item); await SaveImages(item, request.Images, ct); Audit(item, "Updated", "تعديل المهمة", old, JsonSerializer.Serialize(item)); await context.SaveChangesAsync(ct); return Ok(new { success = true, task = item });
    }

    [HttpPost("TogglePin")]
    [HttpPost("/DevelopmentCenter/TogglePin")]
    public async Task<IActionResult> TogglePin([FromForm] int id, CancellationToken ct) { var item = await Find(id, ct); if (item is null) return NotFound(); item.IsPinned = !item.IsPinned; item.PinnedAt = item.IsPinned ? IstanbulTimeHelper.Now : null; Touch(item); Audit(item, item.IsPinned ? "Pinned" : "Unpinned", item.IsPinned ? "تثبيت المهمة" : "إلغاء التثبيت"); await context.SaveChangesAsync(ct); return Ok(new { success = true, item.IsPinned }); }

    [HttpPost("Delete")]
    [HttpPost("/DevelopmentCenter/Delete")]
    public async Task<IActionResult> Delete([FromForm] int id, CancellationToken ct) { var item = await Find(id, ct); if (item is null) return NotFound(); item.IsDeleted = true; item.IsPinned = false; item.PinnedAt = null; item.DeletedAt = IstanbulTimeHelper.Now; item.DeletedByUserId = User.GetUserId(); item.DeletedByName = User.Identity?.Name; Touch(item); Audit(item, "Deleted", "حذف المهمة"); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("RestoreDeletedTask")]
    [HttpPost("/DevelopmentCenter/RestoreDeletedTask")]
    public async Task<IActionResult> RestoreDeletedTask([FromForm] int id, CancellationToken ct) { var item = await context.SystemDevelopmentTasks.FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted, ct); if (item is null) return NotFound(); item.IsDeleted = false; item.DeletedAt = null; item.DeletedByUserId = item.DeletedByName = null; item.SortOrder = await NextOrder(item.Category, ct); Touch(item); Audit(item, "Restored", "استعادة المهمة"); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("RestoreAllDeletedTasks")]
    [HttpPost("/DevelopmentCenter/RestoreAllDeletedTasks")]
    public async Task<IActionResult> RestoreAllDeletedTasks(CancellationToken ct) { var items = await context.SystemDevelopmentTasks.Where(item => item.IsDeleted).ToListAsync(ct); var order = await NextOrder(1, ct); foreach (var item in items) { item.IsDeleted = false; item.DeletedAt = null; item.DeletedByUserId = item.DeletedByName = null; item.SortOrder = order++; Touch(item); Audit(item, "Restored", "استعادة المهمة"); } await context.SaveChangesAsync(ct); return Ok(new { success = true, restored = items.Count }); }

    [HttpPost("ToggleCompleted")]
    [HttpPost("/DevelopmentCenter/ToggleCompleted")]
    public async Task<IActionResult> ToggleCompleted([FromForm] int id, [FromForm] bool completed, CancellationToken ct) { var item = await Find(id, ct); if (item is null) return NotFound(); if (completed) { item.PreviousCategory = IsActiveCategory(item.Category) ? item.Category : (byte)1; item.Category = 4; item.IsCompleted = true; } else { item.Category = IsActiveCategory(item.PreviousCategory) ? item.PreviousCategory!.Value : (byte)1; item.PreviousCategory = null; item.IsCompleted = false; } item.SortOrder = await NextOrder(item.Category, ct); var assignment = await context.DevelopmentTaskAssignments.FirstOrDefaultAsync(row => row.TaskId == id, ct); if (assignment is not null) { assignment.DeveloperStatus = completed ? (byte)2 : (byte)0; assignment.CompletedAt = completed ? DateTimeOffset.UtcNow : null; } Touch(item); Audit(item, completed ? "Completed" : "Reopened", completed ? "إنهاء المهمة" : "إعادة فتح المهمة"); await context.SaveChangesAsync(ct); return Ok(new { success = true, completed }); }

    [HttpPost("RejectReview")]
    [HttpPost("/DevelopmentCenter/RejectReview")]
    public async Task<IActionResult> RejectReview([FromForm] int id, CancellationToken ct) { var item = await Find(id, ct); var assignment = await context.DevelopmentTaskAssignments.FirstOrDefaultAsync(row => row.TaskId == id, ct); if (item is null || assignment is null) return NotFound(); assignment.DeveloperStatus = 1; assignment.CompletedAt = null; item.Category = 2; item.IsCompleted = false; item.SortOrder = await NextOrder(2, ct); context.DevelopmentTaskComments.Add(new DevelopmentTaskComment { TaskId = id, EmployeeId = assignment.EmployeeId, EmployeeName = User.Identity?.Name ?? "الإدارة", CommentText = "لقد تم رفض شغلك، يرجى إعادته بتركيز.", CreatedAt = DateTimeOffset.UtcNow }); Touch(item); Audit(item, "RejectedForRework", "رفض المراجعة"); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("ChangeInProgressTaskCategory")]
    [HttpPost("/DevelopmentCenter/ChangeInProgressTaskCategory")]
    public async Task<IActionResult> ChangeInProgressTaskCategory([FromForm] int id, [FromForm] int targetCategory, CancellationToken ct) { var item = await Find(id, ct); var category = NormalizeCategory(targetCategory); if (item is null || !IsActiveCategory(category)) return BadRequest(); item.Category = category; item.SortOrder = await NextOrder(category, ct); Touch(item); Audit(item, "CategoryChanged", "تغيير تصنيف المهمة"); await AutoAssign(item, ct); await context.SaveChangesAsync(ct); return Ok(new { success = true, category }); }

    [HttpPost("Reorder")]
    [HttpPost("/DevelopmentCenter/Reorder")]
    public async Task<IActionResult> Reorder([FromForm] int category, [FromForm] string? orderedIds, CancellationToken ct) { var normalized = NormalizeCategory(category); var ids = (orderedIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).Distinct().ToArray(); var rows = await context.SystemDevelopmentTasks.Where(item => ids.Contains(item.Id) && item.Category == normalized && !item.IsDeleted).ToListAsync(ct); if (rows.Count != ids.Length) return BadRequest(); for (var index = 0; index < ids.Length; index++) rows.Single(item => item.Id == ids[index]).SortOrder = index; await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("AdvanceTaskState")]
    [HttpPost("/DevelopmentCenter/AdvanceTaskState")]
    public async Task<IActionResult> AdvanceTaskState([FromForm] int id, CancellationToken ct) { var item = await Find(id, ct); if (item is null) return NotFound(); var next = item.Category switch { 1 => (byte)2, 2 => (byte)3, 3 => (byte)4, 6 => (byte)2, _ => item.Category }; if (next == 4) { item.PreviousCategory = item.Category; item.IsCompleted = true; } item.Category = next; item.SortOrder = await NextOrder(next, ct); Touch(item); Audit(item, "Advanced", "تقديم حالة المهمة"); await context.SaveChangesAsync(ct); return Ok(new { success = true, category = next }); }

    [HttpPost("MoveToReview")]
    [HttpPost("/DevelopmentCenter/MoveToReview")]
    public async Task<IActionResult> MoveToReview([FromForm] int id, CancellationToken ct) { var item = await Find(id, ct); if (item is null) return NotFound(); item.Category = 3; item.SortOrder = await NextOrder(3, ct); var assignment = await context.DevelopmentTaskAssignments.FirstOrDefaultAsync(row => row.TaskId == id, ct); if (assignment is not null) assignment.DeveloperStatus = 3; Touch(item); Audit(item, "MovedToReview", "نقل للمراجعة"); await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    private async Task<SystemDevelopmentTask?> Find(int id, CancellationToken ct) => await context.SystemDevelopmentTasks.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, ct);
    private async Task<int> NextOrder(byte category, CancellationToken ct) => (await context.SystemDevelopmentTasks.Where(item => item.Category == category && !item.IsDeleted).MaxAsync(item => (int?)item.SortOrder, ct) ?? -1) + 1;
    private async Task SaveImages(SystemDevelopmentTask task, IEnumerable<IFormFile>? files, CancellationToken ct) { var index = task.Images.Count; foreach (var file in files ?? []) { if (file.Length is <= 0 or > 15 * 1024 * 1024 || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue; var stored = await storage.UploadAsync(file, "development-tasks", User.GetUserId(), ct); task.Images.Add(new SystemDevelopmentTaskImage { ImageUrl = stored.PublicUrl ?? $"/api/v1/media/{Uri.EscapeDataString(stored.Key)}", OriginalFileName = Path.GetFileName(file.FileName), SortOrder = index++, CreatedAt = IstanbulTimeHelper.Now }); } }
    private async Task AutoAssign(SystemDevelopmentTask task, CancellationToken ct) { var rule = await context.DevelopmentTaskCategoryAssignmentRules.AsNoTracking().FirstOrDefaultAsync(item => item.Category == task.Category, ct); if (rule is null) return; var assignment = await context.DevelopmentTaskAssignments.FirstOrDefaultAsync(item => item.TaskId == task.Id, ct); if (assignment is null) { context.DevelopmentTaskAssignments.Add(new DevelopmentTaskAssignment { TaskId = task.Id, EmployeeId = rule.EmployeeId, EmployeeName = rule.EmployeeName, AssignedAt = DateTimeOffset.UtcNow, AssignedByUserId = User.GetUserId(), AssignedByName = User.Identity?.Name }); } else { assignment.EmployeeId = rule.EmployeeId; assignment.EmployeeName = rule.EmployeeName; assignment.AssignedAt = DateTimeOffset.UtcNow; } }
    private void Audit(SystemDevelopmentTask item, string type, string text, string? oldJson = null, string? newJson = null) => context.SystemDevelopmentTaskAuditLogs.Add(new SystemDevelopmentTaskAuditLog { DevelopmentTaskId = item.Id, TaskTitle = item.Title, ActionType = type, ActionText = text, OldDataJson = oldJson, NewDataJson = newJson, ChangedAt = IstanbulTimeHelper.Now, ChangedByUserId = User.GetUserId(), ChangedByName = User.Identity?.Name });
    private void Touch(SystemDevelopmentTask item) { item.UpdatedAt = IstanbulTimeHelper.Now; item.UpdatedByUserId = User.GetUserId(); item.UpdatedByName = User.Identity?.Name; }
    private static string? Validate(DevelopmentTaskRequest request) { if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 180) return "عنوان المهمة مطلوب ولا يزيد عن 180 حرف."; if (!IsActiveCategory(NormalizeCategory(request.Category))) return "تصنيف المهمة غير صحيح."; if (request.EstimatedMinutes is < 1 or > 100_000) return "المدة المتوقعة غير صحيحة."; return null; }
    private static byte NormalizeCategory(int value) => value is 1 or 2 or 3 or 4 or 6 ? (byte)value : (byte)1;
    private static bool IsActiveCategory(byte? value) => value is 1 or 2 or 3 or 6;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class DevelopmentTaskRequest
{
    public int Id { get; set; } public string Title { get; set; } = string.Empty; public string? Description { get; set; } public int Category { get; set; } = 1; public int? EstimatedMinutes { get; set; } public List<IFormFile> Images { get; set; } = [];
}
