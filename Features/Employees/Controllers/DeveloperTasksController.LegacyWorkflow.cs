using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

public sealed partial class DeveloperTasksController
{
    [HttpGet("Index")]
    [HttpGet("/DeveloperTasks/Index")]
    [HttpPost("/DeveloperTasks/Index")]
    public Task<IActionResult> Index(CancellationToken ct) => Snapshot(ct);

    [HttpGet("Snapshot")]
    [HttpGet("/DeveloperTasks/Snapshot")]
    public async Task<IActionResult> Snapshot(CancellationToken ct)
    {
        var employee = await CurrentEmployee(ct); if (employee is null) return Unauthorized();
        var assignments = await context.DevelopmentTaskAssignments.AsNoTracking().Where(item => item.EmployeeId == employee.Id).OrderByDescending(item => item.AssignedAt).Take(500).ToListAsync(ct);
        var ids = assignments.Select(item => item.TaskId).ToArray(); var tasks = await context.SystemDevelopmentTasks.AsNoTracking().Include(item => item.Images).Where(item => ids.Contains(item.Id) && !item.IsDeleted).ToListAsync(ct); var comments = await context.DevelopmentTaskComments.AsNoTracking().Where(item => ids.Contains(item.TaskId)).OrderBy(item => item.CreatedAt).ToListAsync(ct); var submissions = await context.DevelopmentTaskReviewSubmissions.AsNoTracking().Include(item => item.Files).Where(item => item.EmployeeId == employee.Id && ids.Contains(item.TaskId)).ToListAsync(ct);
        return Ok(new { employee = new { employee.Id, employee.Name, employee.ImageUrl }, tasks, assignments, comments, submissions });
    }

    [HttpGet("RejectedWorkNotifications")]
    [HttpGet("/DeveloperTasks/RejectedWorkNotifications")]
    public async Task<IActionResult> RejectedWorkNotifications(CancellationToken ct)
    {
        var employee = await CurrentEmployee(ct); if (employee is null) return Unauthorized();
        var taskIds = context.DevelopmentTaskAssignments.Where(item => item.EmployeeId == employee.Id && item.DeveloperStatus == 1).Select(item => item.TaskId);
        var rows = await context.DevelopmentTaskComments.AsNoTracking().Where(item => taskIds.Contains(item.TaskId) && item.CommentText.Contains("رفض")).OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync(ct); return Ok(rows);
    }

    [HttpPost("Start")]
    [HttpPost("MoveToProgress")]
    [HttpPost("/DeveloperTasks/MoveToProgress")]
    public async Task<IActionResult> MoveToProgress([FromForm] int id, CancellationToken ct)
    {
        var tuple = await OwnAssignment(id, ct); if (tuple.assignment is null || tuple.task is null) return BadRequest(new { success = false, message = "يمكن تعديل المهام المسندة إليك فقط." }); tuple.assignment.DeveloperStatus = 1; tuple.task.Category = 2; tuple.task.IsCompleted = false; tuple.task.UpdatedAt = IstanbulTimeHelper.Now; tuple.task.UpdatedByUserId = User.GetUserId(); await context.SaveChangesAsync(ct); return Ok(new { success = true });
    }

    [HttpPost("StartTimer")]
    [HttpPost("/DeveloperTasks/StartTimer")]
    public async Task<IActionResult> StartTimer([FromForm] int id, CancellationToken ct) { var tuple = await OwnAssignment(id, ct); if (tuple.assignment is null) return BadRequest(); tuple.assignment.DeveloperStatus = 1; tuple.assignment.StartedAt ??= DateTimeOffset.UtcNow; tuple.assignment.TimerStartedManually = true; await context.SaveChangesAsync(ct); return Ok(new { success = true, startedAt = tuple.assignment.StartedAt }); }

    [HttpPost("AddComment")]
    [HttpPost("/DeveloperTasks/AddComment")]
    public async Task<IActionResult> AddComment([FromForm] int taskId, [FromForm] string? commentText, CancellationToken ct) { var employee = await CurrentEmployee(ct); if (employee is null || !await context.DevelopmentTaskAssignments.AnyAsync(item => item.TaskId == taskId && item.EmployeeId == employee.Id, ct)) return BadRequest(); var text = commentText?.Trim(); if (string.IsNullOrWhiteSpace(text) || text.Length > 2000) return BadRequest(new { message = "اكتب تعليقًا حتى 2000 حرف." }); var item = new DevelopmentTaskComment { TaskId = taskId, EmployeeId = employee.Id, EmployeeName = employee.Name ?? "موظف", CommentText = text, CreatedAt = DateTimeOffset.UtcNow }; context.DevelopmentTaskComments.Add(item); await context.SaveChangesAsync(ct); return Ok(new { success = true, comment = item }); }

    [HttpPost("UpdateTask")]
    [HttpPost("/DeveloperTasks/UpdateTask")]
    public async Task<IActionResult> UpdateTask([FromForm] int taskId, [FromForm] string? title, [FromForm] string? description, [FromForm] int? estimatedMinutes, CancellationToken ct) { var tuple = await OwnAssignment(taskId, ct); if (tuple.task is null) return BadRequest(); var normalized = title?.Trim(); if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 180 || description?.Length > 4000 || estimatedMinutes is < 1 or > 100_000) return BadRequest(new { message = "بيانات المهمة غير صحيحة." }); tuple.task.Title = normalized; tuple.task.Description = description?.Trim(); tuple.task.EstimatedMinutes = estimatedMinutes; tuple.task.UpdatedAt = IstanbulTimeHelper.Now; tuple.task.UpdatedByUserId = User.GetUserId(); tuple.task.UpdatedByName = User.Identity?.Name; await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("CreateOwnTask")]
    [HttpPost("/DeveloperTasks/CreateOwnTask")]
    public async Task<IActionResult> CreateOwnTask([FromForm] string? title, [FromForm] string? description, [FromForm] int? estimatedMinutes, CancellationToken ct) { var employee = await CurrentEmployee(ct); var normalized = title?.Trim(); if (employee is null || string.IsNullOrWhiteSpace(normalized) || normalized.Length > 180 || description?.Length > 4000) return BadRequest(); var order = (await context.SystemDevelopmentTasks.Where(item => item.Category == 2 && !item.IsDeleted).MaxAsync(item => (int?)item.SortOrder, ct) ?? -1) + 1; var task = new SystemDevelopmentTask { Title = normalized, Description = description?.Trim(), Category = 2, SortOrder = order, EstimatedMinutes = estimatedMinutes, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name }; context.SystemDevelopmentTasks.Add(task); await context.SaveChangesAsync(ct); context.DevelopmentTaskAssignments.Add(new DevelopmentTaskAssignment { TaskId = task.Id, EmployeeId = employee.Id, EmployeeName = employee.Name ?? "موظف", AssignedAt = DateTimeOffset.UtcNow, AssignedByUserId = User.GetUserId(), AssignedByName = User.Identity?.Name, DeveloperStatus = 1 }); await context.SaveChangesAsync(ct); return Ok(new { success = true, id = task.Id }); }

    [HttpPost("SubmitMarketingReport")]
    [HttpPost("/DeveloperTasks/SubmitMarketingReport")]
    public async Task<IActionResult> SubmitMarketingReport([FromForm] string? reportType, [FromForm] string? reportText, CancellationToken ct) { var employee = await CurrentEmployee(ct); var text = reportText?.Trim(); var type = reportType?.Trim().ToLowerInvariant(); if (employee is null || string.IsNullOrWhiteSpace(text) || text.Length > 4000 || type is not ("accomplished" or "not-accomplished")) return BadRequest(); var item = new MarketingWorkReport { EmployeeId = employee.Id, EmployeeName = employee.Name ?? "موظف", IsCompleted = type == "accomplished", ReportText = text, CreatedAt = DateTimeOffset.UtcNow }; context.MarketingWorkReports.Add(item); await context.SaveChangesAsync(ct); return Ok(new { success = true, report = item }); }

    [HttpPost("ReturnToProgress")]
    [HttpPost("/DeveloperTasks/ReturnToProgress")]
    public async Task<IActionResult> ReturnToProgress([FromForm] int taskId, CancellationToken ct) { var tuple = await OwnAssignment(taskId, ct); if (tuple.task is null || tuple.assignment is null) return BadRequest(); tuple.task.Category = 2; tuple.task.IsCompleted = false; tuple.assignment.DeveloperStatus = 1; tuple.assignment.CompletedAt = null; await context.SaveChangesAsync(ct); return Ok(new { success = true }); }

    [HttpPost("SubmitForReview")]
    [HttpPost("/DeveloperTasks/SubmitForReview")]
    [RequestSizeLimit(120L * 1024 * 1024)]
    public async Task<IActionResult> SubmitForReview([FromForm] int taskId, [FromForm] List<IFormFile>? attachments, [FromServices] S3StorageService storage, CancellationToken ct)
    {
        var tuple = await OwnAssignment(taskId, ct); if (tuple.task is null || tuple.assignment is null) return BadRequest(); var files = (attachments ?? []).Where(file => file.Length > 0).Take(10).ToList(); if (files.Any(file => file.Length > 20L * 1024 * 1024)) return BadRequest(new { message = "حجم الملف أكبر من المسموح." });
        var submission = await context.DevelopmentTaskReviewSubmissions.Include(item => item.Files).FirstOrDefaultAsync(item => item.TaskId == taskId, ct); if (submission is null) { submission = new DevelopmentTaskReviewSubmission { TaskId = taskId, EmployeeId = tuple.assignment.EmployeeId }; context.DevelopmentTaskReviewSubmissions.Add(submission); } else { foreach (var old in submission.Files) { try { await storage.DeleteAsync(old.FilePath, ct); } catch { } } context.DevelopmentTaskReviewFiles.RemoveRange(submission.Files); }
        submission.SubmittedAt = DateTimeOffset.UtcNow; submission.SubmissionType = "Review"; var order = 0; foreach (var file in files) { var stored = await storage.UploadAsync(file, "development-review", User.GetUserId(), ct); submission.Files.Add(new DevelopmentTaskReviewFile { OriginalFileName = Path.GetFileName(file.FileName), StoredFileName = Path.GetFileName(stored.Key), FilePath = stored.Key, ContentType = file.ContentType, FileSize = file.Length, SortOrder = order++, CreatedAt = DateTimeOffset.UtcNow }); }
        tuple.task.Category = 3; tuple.assignment.DeveloperStatus = 3; await context.SaveChangesAsync(ct); return Ok(new { success = true, files = submission.Files.Count });
    }

    [HttpGet("ReviewFile/{id:int}")]
    [HttpGet("/DeveloperTasks/ReviewFile/{id:int}")]
    public Task<IActionResult> DownloadReviewFile(int id, [FromServices] S3StorageService storage, CancellationToken ct) => DownloadFile(id, false, storage, ct);

    [HttpGet("LegacyReviewFile/{id:int}")]
    [HttpGet("/DeveloperTasks/LegacyReviewFile/{id:int}")]
    public Task<IActionResult> DownloadLegacyReviewFile(int id, [FromServices] S3StorageService storage, CancellationToken ct) => DownloadFile(id, true, storage, ct);

    private async Task<IActionResult> DownloadFile(int id, bool legacy, S3StorageService storage, CancellationToken ct)
    {
        var employee = await CurrentEmployee(ct); if (employee is null) return Unauthorized(); string? key; string? name; string? type;
        if (legacy) { var item = await context.DevelopmentTaskReviewSubmissions.AsNoTracking().FirstOrDefaultAsync(row => row.Id == id && row.EmployeeId == employee.Id, ct); key = item?.FilePath; name = item?.OriginalFileName; type = item?.ContentType; }
        else { var item = await context.DevelopmentTaskReviewFiles.AsNoTracking().Include(row => row.Submission).FirstOrDefaultAsync(row => row.Id == id && row.Submission!.EmployeeId == employee.Id, ct); key = item?.FilePath; name = item?.OriginalFileName; type = item?.ContentType; }
        if (string.IsNullOrWhiteSpace(key)) return NotFound(); var (bytes, contentType) = await storage.DownloadAsync(key, ct); return File(bytes, type ?? contentType ?? "application/octet-stream", name ?? "review-file");
    }

    private async Task<Employee?> CurrentEmployee(CancellationToken ct) { var userId = User.GetUserId(); return string.IsNullOrWhiteSpace(userId) ? null : await context.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.ApplicationUserId == userId, ct); }
    private async Task<(DevelopmentTaskAssignment? assignment, SystemDevelopmentTask? task)> OwnAssignment(int taskId, CancellationToken ct) { var employee = await CurrentEmployee(ct); if (employee is null) return (null, null); var assignment = await context.DevelopmentTaskAssignments.FirstOrDefaultAsync(item => item.TaskId == taskId && item.EmployeeId == employee.Id, ct); var task = assignment is null ? null : await context.SystemDevelopmentTasks.FirstOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted, ct); return (assignment, task); }
}
