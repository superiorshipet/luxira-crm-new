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
[Route("api/v1/employees/breaks")]
[Route("Break")]
public class BreakController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BreakController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("start")]
    public async Task<ActionResult<EmployeeBreakDto>> StartBreak([FromBody] StartBreakRequest request, CancellationToken ct)
    {
        var b = new EmployeeBreak
        {
            EmployeeId = request.EmployeeId,
            StartTime = DateTime.UtcNow,
            Reason = request.Reason
        };

        await _context.EmployeeBreaks.AddAsync(b, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new EmployeeBreakDto(b.Id, b.EmployeeId, b.StartTime, b.EndTime, b.Reason));
    }

    [HttpPost("{id:int}/end")]
    public async Task<ActionResult<EmployeeBreakDto>> EndBreak([FromRoute] int id, CancellationToken ct)
    {
        var b = await _context.EmployeeBreaks.FindAsync([id], ct);
        if (b == null)
        {
            throw new NotFoundException($"Break record {id} not found.");
        }

        b.EndTime = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Ok(new EmployeeBreakDto(b.Id, b.EmployeeId, b.StartTime, b.EndTime, b.Reason));
    }
}

public record EmployeeBreakDto(int Id, int EmployeeId, DateTime StartTime, DateTime? EndTime, string? Reason);
public record StartBreakRequest(int EmployeeId, string? Reason);
