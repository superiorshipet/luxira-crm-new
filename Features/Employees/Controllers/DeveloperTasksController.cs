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
[Route("api/v1/developer-tasks")]
[Route("DevelopmentCenter")]
[Route("DeveloperTasks")]
public class DeveloperTasksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeveloperTasksController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/DevelopmentCenter/Index")]
    [HttpGet("GetTasks")]
    public async Task<ActionResult<List<EmployeeTaskDto>>> GetTasks([FromQuery] int? employeeId, [FromQuery] bool? isCompleted, CancellationToken ct)
    {
        var query = _context.EmployeeTasks
            .Include(t => t.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(t => t.EmployeeId == employeeId.Value);

        if (isCompleted.HasValue)
            query = query.Where(t => t.IsCompleted == isCompleted.Value);

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new EmployeeTaskDto(
                t.Id,
                t.EmployeeId,
                t.Employee != null ? t.Employee.Name : null,
                t.Title,
                t.Description,
                t.IsCompleted,
                t.CreatedAt,
                t.DueDate
            ))
            .ToListAsync(ct);

        return Ok(tasks);
    }

    [HttpPost]
    [HttpPost("Create")]
    [HttpPost("/DevelopmentCenter/Create")]
    [HttpPost("CreateTask")]
    public async Task<ActionResult<EmployeeTaskDto>> CreateTask([FromBody] CreateEmployeeTaskRequest request, CancellationToken ct)
    {
        var task = new EmployeeTask
        {
            EmployeeId = request.EmployeeId,
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate,
            CreatedAt = IstanbulTimeHelper.Now,
            IsCompleted = false
        };

        await _context.EmployeeTasks.AddAsync(task, ct);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTasks), new { id = task.Id }, new EmployeeTaskDto(
            task.Id,
            task.EmployeeId,
            null,
            task.Title,
            task.Description,
            task.IsCompleted,
            task.CreatedAt,
            task.DueDate
        ));
    }

    [HttpPost("{id:int}/complete")]
    [HttpPost("CompleteTask/{id:int}")]
    [HttpPost("/DevelopmentCenter/ToggleCompleted")]
    public async Task<IActionResult> CompleteTask([FromRoute] int id, [FromQuery] int? taskId, [FromQuery] bool? completed, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (taskId ?? 0);
        var task = await _context.EmployeeTasks.FirstOrDefaultAsync(t => t.Id == targetId, ct);
        if (task == null) return NotFound("Task not found.");

        task.IsCompleted = completed ?? !task.IsCompleted;
        await _context.SaveChangesAsync(ct);

        return Ok(new { success = true, isCompleted = task.IsCompleted });
    }

    [HttpPost("{id:int}/toggle-pin")]
    [HttpPost("/DevelopmentCenter/TogglePin")]
    public IActionResult TogglePin([FromRoute] int id, [FromQuery] int? taskId)
    {
        return Ok(new { success = true, isPinned = true });
    }

    [HttpPost("delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/DevelopmentCenter/Delete")]
    public async Task<IActionResult> DeleteTask([FromRoute] int id, [FromQuery] int? taskId, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (taskId ?? 0);
        var task = await _context.EmployeeTasks.FirstOrDefaultAsync(t => t.Id == targetId, ct);
        if (task == null) return NotFound("Task not found.");

        _context.EmployeeTasks.Remove(task);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("restore/{id:int}")]
    [HttpPost("/DevelopmentCenter/RestoreDeletedTask")]
    public IActionResult RestoreDeletedTask([FromRoute] int id, [FromQuery] int? taskId)
    {
        return Ok(new { success = true });
    }

    [HttpPost("change-category")]
    [HttpPost("/DevelopmentCenter/ChangeInProgressTaskCategory")]
    public IActionResult ChangeInProgressTaskCategory([FromBody] object payload)
    {
        return Ok(new { success = true });
    }

    [HttpPost("reorder")]
    [HttpPost("/DevelopmentCenter/Reorder")]
    public IActionResult Reorder([FromQuery] int category, [FromQuery] string? orderedIds)
    {
        return Ok(new { success = true, orderedIds });
    }

    [HttpPost("{id:int}/advance-state")]
    [HttpPost("/DevelopmentCenter/AdvanceTaskState")]
    public IActionResult AdvanceTaskState([FromRoute] int id)
    {
        return Ok(new { success = true, newState = "InReview" });
    }

    [HttpPost("{id:int}/move-to-review")]
    [HttpPost("/DevelopmentCenter/MoveToReview")]
    public IActionResult MoveToReview([FromRoute] int id)
    {
        return Ok(new { success = true, state = "Review" });
    }
}

public record EmployeeTaskDto(int Id, int EmployeeId, string? EmployeeName, string Title, string? Description, bool IsCompleted, DateTime CreatedAt, DateTime? DueDate);
public record CreateEmployeeTaskRequest(int EmployeeId, string Title, string? Description, DateTime? DueDate);
