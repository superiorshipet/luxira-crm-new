using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/tasks")]
[Route("DeveloperTasks")]
[Route("DevelopmentCenter")]
public class DeveloperTasksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeveloperTasksController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetTasks")]
    public async Task<ActionResult<List<EmployeeTaskDto>>> GetTasks([FromQuery] int? employeeId, [FromQuery] bool? isCompleted, CancellationToken ct)
    {
        var query = _context.EmployeeTasks
            .Include(t => t.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(t => t.EmployeeId == employeeId.Value);
        }

        if (isCompleted.HasValue)
        {
            query = query.Where(t => t.IsCompleted == isCompleted.Value);
        }

        var tasks = await query.OrderByDescending(t => t.CreatedAt)
            .Select(t => new EmployeeTaskDto(
                t.Id,
                t.EmployeeId,
                t.Employee != null ? t.Employee.Name : null,
                t.Title,
                t.Description,
                t.IsCompleted,
                t.CreatedAt,
                t.DueDate))
            .ToListAsync(ct);

        return Ok(tasks);
    }

    [HttpPost]
    [HttpPost("CreateTask")]
    public async Task<ActionResult<EmployeeTaskDto>> CreateTask([FromBody] CreateEmployeeTaskRequest request, CancellationToken ct)
    {
        var task = new EmployeeTask
        {
            EmployeeId = request.EmployeeId,
            Title = request.Title,
            Description = request.Description,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            DueDate = request.DueDate
        };

        await _context.EmployeeTasks.AddAsync(task, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new EmployeeTaskDto(task.Id, task.EmployeeId, null, task.Title, task.Description, task.IsCompleted, task.CreatedAt, task.DueDate));
    }

    [HttpPost("{id:int}/complete")]
    [HttpPost("CompleteTask/{id:int}")]
    public async Task<IActionResult> CompleteTask([FromRoute] int id, CancellationToken ct)
    {
        var task = await _context.EmployeeTasks.FindAsync([id], ct);
        if (task == null)
        {
            throw new NotFoundException($"Task {id} not found.");
        }

        task.IsCompleted = true;
        await _context.SaveChangesAsync(ct);

        return Ok(new { isCompleted = true, message = "Task completed successfully." });
    }
}

public record EmployeeTaskDto(int Id, int EmployeeId, string? EmployeeName, string Title, string? Description, bool IsCompleted, DateTime CreatedAt, DateTime? DueDate);
public record CreateEmployeeTaskRequest(int EmployeeId, string Title, string? Description, DateTime? DueDate);
