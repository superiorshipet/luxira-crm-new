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
[Route("DeveloperTasks")]
public sealed class DeveloperTasksController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    [HttpGet("GetTasks")]
    public async Task<ActionResult<List<EmployeeTaskDto>>> GetTasks(
        [FromQuery] int? employeeId,
        [FromQuery] bool? isCompleted,
        CancellationToken ct)
    {
        string? employeeUserId = null;
        if (employeeId is > 0)
        {
            employeeUserId = await context.Employees.AsNoTracking()
                .Where(employee => employee.Id == employeeId)
                .Select(employee => employee.ApplicationUserId)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(employeeUserId)) return Ok(new List<EmployeeTaskDto>());
        }

        var query = context.EmployeeTasks.AsNoTracking().AsQueryable();
        if (employeeUserId is not null)
            query = query.Where(task => task.Assignments.Any(a => a.EmployeeUserId == employeeUserId));
        if (isCompleted == true)
            query = query.Where(task => task.Assignments.Any() && task.Assignments.All(a => a.CompletedAt != null));
        else if (isCompleted == false)
            query = query.Where(task => task.Assignments.Any(a => a.CompletedAt == null));

        var tasks = await query
            .OrderByDescending(task => task.CreatedAt)
            .Take(500)
            .Select(task => new EmployeeTaskDto(
                task.Id,
                task.Title,
                task.Description,
                task.DurationMinutes,
                task.Priority,
                task.CreatedByUserId,
                task.CreatedByName,
                task.CreatedAt,
                task.Assignments.Select(assignment => new EmployeeTaskAssignmentDto(
                    assignment.Id,
                    assignment.EmployeeUserId,
                    assignment.EmployeeName,
                    assignment.Status,
                    assignment.AssignedAt,
                    assignment.DueAt,
                    assignment.CompletedAt,
                    assignment.CompletionNote)).ToList()))
            .ToListAsync(ct);
        return Ok(tasks);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,TeamLeader")]
    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<EmployeeTaskDto>> CreateTask(
        [FromBody] CreateEmployeeTaskRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BadRequestException("Task title is required.");
        if (request.Title.Length > 200 || request.Description?.Length > 2000)
            throw new BadRequestException("Task title or description is too long.");

        var employee = await context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.EmployeeId, ct);
        if (employee is null || string.IsNullOrWhiteSpace(employee.ApplicationUserId))
            throw new NotFoundException("Employee account was not found.");

        var now = IstanbulTimeHelper.Now;
        var task = new EmployeeTask
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            DurationMinutes = Math.Max(0, request.DurationMinutes),
            Priority = NormalizePriority(request.Priority),
            CreatedByUserId = User.GetUserId() ?? "system",
            CreatedByName = User.Identity?.Name,
            CreatedAt = now
        };
        task.Assignments.Add(new EmployeeTaskAssignment
        {
            EmployeeUserId = employee.ApplicationUserId,
            EmployeeName = employee.Name,
            EmployeeImageUrl = employee.ImageUrl,
            AssignedAt = now,
            DueAt = request.DueAt,
            Status = "New"
        });

        await context.EmployeeTasks.AddAsync(task, ct);
        await context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetTasks), new { employeeId = request.EmployeeId }, Map(task));
    }

    [HttpPost("{id:int}/complete")]
    [HttpPost("CompleteTask/{id:int}")]
    public async Task<IActionResult> CompleteTask(
        [FromRoute] int id,
        [FromBody] CompleteEmployeeTaskRequest? request,
        CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var isManager = User.IsInRole("Admin") || User.IsInRole("Administrator") ||
            User.IsInRole("ExecutiveDirector") || User.IsInRole("TeamLeader");
        var assignments = await context.EmployeeTaskAssignments
            .Where(assignment => assignment.EmployeeTaskId == id &&
                (isManager || assignment.EmployeeUserId == userId))
            .ToListAsync(ct);
        if (assignments.Count == 0)
            throw new NotFoundException("Task assignment was not found.");

        var now = IstanbulTimeHelper.Now;
        foreach (var assignment in assignments)
        {
            assignment.CompletedAt = now;
            assignment.CompletionNote = request?.CompletionNote?.Trim();
            assignment.Status = "Completed";
        }
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, completedAssignments = assignments.Count });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask([FromRoute] int id, CancellationToken ct)
    {
        var task = await context.EmployeeTasks.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (task is null) throw new NotFoundException("Task was not found.");
        context.EmployeeTasks.Remove(task);
        await context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string NormalizePriority(string? priority) =>
        string.IsNullOrWhiteSpace(priority) ? "Important" : priority.Trim()[..Math.Min(30, priority.Trim().Length)];

    private static EmployeeTaskDto Map(EmployeeTask task) => new(
        task.Id,
        task.Title,
        task.Description,
        task.DurationMinutes,
        task.Priority,
        task.CreatedByUserId,
        task.CreatedByName,
        task.CreatedAt,
        task.Assignments.Select(assignment => new EmployeeTaskAssignmentDto(
            assignment.Id,
            assignment.EmployeeUserId,
            assignment.EmployeeName,
            assignment.Status,
            assignment.AssignedAt,
            assignment.DueAt,
            assignment.CompletedAt,
            assignment.CompletionNote)).ToList());
}

public record EmployeeTaskDto(
    int Id,
    string Title,
    string? Description,
    int DurationMinutes,
    string Priority,
    string CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAt,
    IReadOnlyList<EmployeeTaskAssignmentDto> Assignments);

public record EmployeeTaskAssignmentDto(
    int Id,
    string EmployeeUserId,
    string? EmployeeName,
    string Status,
    DateTime AssignedAt,
    DateTime? DueAt,
    DateTime? CompletedAt,
    string? CompletionNote);

public record CreateEmployeeTaskRequest(
    int EmployeeId,
    string Title,
    string? Description,
    int DurationMinutes = 0,
    string? Priority = null,
    DateTime? DueAt = null);

public record CompleteEmployeeTaskRequest(string? CompletionNote);
