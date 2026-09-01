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
[Route("api/v1/employees/errors")]
[Route("EmployeeErrors")]
[Route("Violations")]
public class EmployeeErrorsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeeErrorsController(ApplicationDbContext context)
    {
        _context = context;
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
}

public record EmployeeViolationDto(int Id, int EmployeeId, string? EmployeeName, string Title, string Description, decimal PenaltyAmount, DateTime OccurredAt, string IssuedByUserId);
public record ReportViolationRequest(int EmployeeId, string Title, string Description, decimal PenaltyAmount);
