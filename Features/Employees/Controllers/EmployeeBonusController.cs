using Luxira.Api.Data;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Employees.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/bonuses")]
[Route("EmployeeBonus")]
[Route("api/[controller]")]
public class EmployeeBonusController : ControllerBase
{
    private readonly EmployeeService _service;
    private readonly ApplicationDbContext _context;

    public EmployeeBonusController(EmployeeService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/EmployeeBonus/Index")]
    [HttpGet("GetBonuses")]
    public async Task<ActionResult<List<EmployeeBonusPaymentDto>>> GetBonuses(
        [FromQuery] int? employeeId,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int? storeId,
        [FromQuery] int? countryId,
        CancellationToken ct)
    {
        var result = await _service.GetBonusPaymentsAsync(employeeId, ct);
        return Ok(result);
    }

    [HttpGet("stats")]
    [HttpGet("/EmployeeBonus/StatsPartial")]
    public async Task<IActionResult> StatsPartial(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int? employeeId,
        [FromQuery] int? storeId,
        [FromQuery] int? countryId,
        CancellationToken ct)
    {
        var totalBonus = await _context.EmployeeBonusPayments.SumAsync(b => (decimal?)b.AmountPaid, ct) ?? 0m;
        var count = await _context.EmployeeBonusPayments.CountAsync(ct);
        return Ok(new { totalBonus, paymentCount = count });
    }

    [HttpGet("details")]
    [HttpGet("/EmployeeBonus/BonusDetails")]
    public async Task<IActionResult> BonusDetails([FromQuery] int employeeId, CancellationToken ct)
    {
        var applicationUserId = await _context.Employees
            .Where(employee => employee.Id == employeeId)
            .Select(employee => employee.ApplicationUserId)
            .FirstOrDefaultAsync(ct);
        var details = await _context.EmployeeBonusPayments
            .Where(b => b.EmployeeId == applicationUserId)
            .OrderByDescending(b => b.DatePaid)
            .ToListAsync(ct);

        return Ok(details);
    }

    [HttpGet("RowsPartial")]
    public async Task<IActionResult> RowsPartial(CancellationToken ct) => Ok(await _context.EmployeeBonusPayments.AsNoTracking()
        .OrderByDescending(item => item.DatePaid).Take(200).ToListAsync(ct));

    [HttpGet("Create")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new
    {
        employees = await _context.Employees.AsNoTracking().Where(item => item.IsActive && item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, Name = item.DisplayName ?? item.Name }).ToListAsync(ct)
    });

    [HttpPost("pay")]
    [HttpPost("/EmployeeBonus/Pay")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> Pay([FromBody] PayBonusEmployeeRequest request, CancellationToken ct)
    {
        var applicationUserId = await _context.Employees
            .Where(employee => employee.Id == request.EmployeeId)
            .Select(employee => employee.ApplicationUserId)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(applicationUserId))
            throw new BadRequestException("Employee is not linked to an application user.");

        var payment = new EmployeeBonusPayment
        {
            EmployeeId = applicationUserId,
            AmountPaid = request.Amount,
            DatePaid = IstanbulTimeHelper.Now
        };

        await _context.EmployeeBonusPayments.AddAsync(payment, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, paymentId = payment.Id, amount = payment.AmountPaid });
    }

    [HttpPost("undo-pay/{paymentId:int}")]
    [HttpPost("/EmployeeBonus/UndoPay")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> UndoPay([RouteOrRequest] int paymentId, [FromQuery] int? id, CancellationToken ct)
    {
        var targetId = paymentId > 0 ? paymentId : (id ?? 0);
        var payment = await _context.EmployeeBonusPayments.FirstOrDefaultAsync(p => p.Id == targetId, ct);
        if (payment == null) return NotFound("Bonus payment not found.");

        _context.EmployeeBonusPayments.Remove(payment);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, removedPaymentId = targetId });
    }

    [HttpGet("archive")]
    [HttpGet("/EmployeeBonus/Archive")]
    public async Task<IActionResult> Archive([FromQuery] int? employeeId, CancellationToken ct)
    {
        var archive = await _context.EmployeeBonusPayments
            .Include(b => b.Employee)
            .OrderByDescending(b => b.DatePaid)
            .Take(100)
            .ToListAsync(ct);

        return Ok(archive);
    }

    [HttpPost("toggle-panel")]
    [HttpPost("/EmployeeBonus/ToggleBonusPanel")]
    public IActionResult ToggleBonusPanel([FromQuery] string employeeId, [FromQuery] bool hidden)
    {
        return Ok(new { success = true, employeeId, hidden });
    }

    [HttpPost]
    [HttpPost("AwardBonus")]
    [HttpPost("Create")]
    [HttpPost("/EmployeeBonus/Create")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<ActionResult<EmployeeBonusPaymentDto>> AwardBonus([FromBody] AwardBonusRequest request, CancellationToken ct)
    {
        var result = await _service.AwardBonusAsync(request, ct);
        return Ok(result);
    }
}

public record PayBonusEmployeeRequest(int EmployeeId, decimal Amount);
