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
[Route("api/v1/employees/violations")]
[Route("Violations")]
public class ViolationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ViolationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("Summary")]
    [HttpGet("/Violations/Index")]
    [HttpGet("/Violations/Summary")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Summary([FromQuery] int? employeeId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct = default)
    {
        var query = _context.EmployeeViolations
            .Include(v => v.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(v => v.EmployeeId == employeeId.Value);

        if (fromDate.HasValue)
            query = query.Where(v => v.OccurredAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(v => v.OccurredAt <= toDate.Value);

        var violations = await query.OrderByDescending(v => v.OccurredAt).ToListAsync(ct);
        return Ok(violations);
    }

    [HttpPost]
    [HttpPost("Create")]
    [HttpPost("/Violations/Create")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Create([FromBody] CreateViolationRequest request, CancellationToken ct)
    {
        var violation = new EmployeeViolation
        {
            EmployeeId = request.EmployeeId,
            Title = request.Title,
            Description = request.Description,
            PenaltyAmount = request.PenaltyAmount,
            OccurredAt = IstanbulTimeHelper.Now,
            IssuedByUserId = User.GetUserId() ?? "Admin"
        };

        await _context.EmployeeViolations.AddAsync(violation, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(violation);
    }
}

public record CreateViolationRequest(int EmployeeId, string Title, string Description, decimal PenaltyAmount);
