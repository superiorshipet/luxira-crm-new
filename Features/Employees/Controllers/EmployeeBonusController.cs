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
[Route("api/v1/employees/bonuses")]
[Route("EmployeeBonus")]
public class EmployeeBonusController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeeBonusController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetBonuses")]
    public async Task<ActionResult<List<EmployeeBonusPaymentDto>>> GetBonuses([FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = _context.EmployeeBonusPayments
            .Include(b => b.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(b => b.EmployeeId == employeeId.Value);
        }

        var list = await query.OrderByDescending(b => b.Date)
            .Select(b => new EmployeeBonusPaymentDto(b.Id, b.EmployeeId, b.Employee != null ? b.Employee.Name : null, b.Amount, b.Date))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [HttpPost("AwardBonus")]
    public async Task<ActionResult<EmployeeBonusPaymentDto>> AwardBonus([FromBody] AwardBonusRequest request, CancellationToken ct)
    {
        var bp = new EmployeeBonusPayment
        {
            EmployeeId = request.EmployeeId,
            Amount = request.Amount,
            Date = DateTime.UtcNow
        };

        await _context.EmployeeBonusPayments.AddAsync(bp, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new EmployeeBonusPaymentDto(bp.Id, bp.EmployeeId, null, bp.Amount, bp.Date));
    }
}

public record EmployeeBonusPaymentDto(int Id, int EmployeeId, string? EmployeeName, decimal Amount, DateTime Date);
public record AwardBonusRequest(int EmployeeId, decimal Amount);
