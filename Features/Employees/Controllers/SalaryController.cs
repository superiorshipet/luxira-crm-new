using System.Security.Claims;
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
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant,Hr")]
[Route("api/v1/salaries")]
[Route("EmployeeSalaries")]
[Route("api/[controller]")]
public class SalaryController : ControllerBase
{
    private readonly EmployeeService _service;
    private readonly ApplicationDbContext _context;

    public SalaryController(EmployeeService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet("payroll")]
    [HttpGet("Index")]
    [HttpGet("/EmployeeSalaries/Index")]
    [HttpGet("/EmployeeSalaries/GetPayrollSummary")]
    [HttpGet("/CallCenterPayroll/GetPayroll")]
    [HttpGet("/CallCenterPayroll/Summary")]
    public async Task<ActionResult<List<PayrollSummaryDto>>> GetPayrollSummary(
        [FromQuery] int? employeeId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        var now = IstanbulTimeHelper.Now;
        int y = year ?? now.Year;
        int m = month ?? now.Month;
        var result = await _service.CalculatePayrollAsync(employeeId, y, m, ct);
        return Ok(result);
    }

    [HttpPost("pay")]
    [HttpPost("/EmployeeSalaries/Pay")]
    [HttpPost("/EmployeeSalaries/ConfirmSalaryPayment")]
    public async Task<ActionResult<SalaryPaymentDto>> PaySalary([FromBody] RecordSalaryPaymentRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var result = await _service.RecordSalaryPaymentAsync(request, userId, ct);
        return Ok(result);
    }

    [HttpGet("payments")]
    [HttpGet("/EmployeeSalaries/GetPayments")]
    public async Task<ActionResult<List<SalaryPaymentDto>>> GetPayments([FromQuery] int? employeeId, CancellationToken ct)
    {
        var result = await _service.GetSalaryPaymentsAsync(employeeId, ct);
        return Ok(result);
    }

    [HttpPost("update-due-amount")]
    [HttpPost("/EmployeeSalaries/UpdateSalaryDueAmount")]
    public IActionResult UpdateSalaryDueAmount([FromBody] UpdateSalaryDueRequest request)
    {
        return NotImplemented("Manual salary due overrides require the full legacy payroll calculation port.");
    }

    [HttpPost("update-days")]
    [HttpPost("/EmployeeSalaries/UpdateSalaryDays")]
    public IActionResult UpdateSalaryDays([FromBody] UpdateSalaryDaysRequest request)
    {
        return NotImplemented("Manual salary-day overrides require the full legacy payroll calculation port.");
    }

    [HttpPost("bulk-confirm")]
    [HttpPost("/EmployeeSalaries/BulkConfirmSalaryPayments")]
    public IActionResult BulkConfirmSalaryPayments([FromBody] BulkSalaryConfirmRequest request, CancellationToken ct)
    {
        return NotImplemented("Bulk payroll confirmation is disabled until all rows can be validated and committed atomically.");
    }

    [HttpPost("delete-payment/{paymentId:int}")]
    [HttpDelete("delete-payment/{paymentId:int}")]
    [HttpPost("/EmployeeSalaries/DeleteSalaryPayment")]
    public async Task<IActionResult> DeleteSalaryPayment([FromRoute] int paymentId, [FromQuery] int? id, CancellationToken ct)
    {
        var targetId = paymentId > 0 ? paymentId : (id ?? 0);
        var payment = await _context.EmployeeSalaryPayments.FirstOrDefaultAsync(p => p.Id == targetId, ct);
        if (payment == null) return NotFound("Salary payment record not found.");

        if (!payment.IsPaid || payment.IsPermanentlyDeleted)
            throw new BadRequestException("Only an active paid salary can be moved to trash.");
        if (payment.IsDeleted) return Ok(new { success = true, deletedId = targetId });

        payment.IsDeleted = true;
        payment.DeletedAt = IstanbulTimeHelper.Now;
        payment.DeletedByUserId = User.GetUserId();
        payment.DeletedByUserName = User.Identity?.Name;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, deletedId = targetId });
    }

    [HttpPost("bulk-delete")]
    [HttpPost("/EmployeeSalaries/BulkDeleteSalaryPayments")]
    public async Task<IActionResult> BulkDeleteSalaryPayments([FromBody] List<int> paymentIds, CancellationToken ct)
    {
        var ids = paymentIds.Where(id => id > 0).Distinct().ToList();
        var payments = await _context.EmployeeSalaryPayments
            .Where(p => ids.Contains(p.Id) && p.IsPaid && !p.IsDeleted && !p.IsPermanentlyDeleted)
            .ToListAsync(ct);
        if (payments.Count != ids.Count)
            throw new BadRequestException("Every selected salary must exist and be an active paid salary.");

        var now = IstanbulTimeHelper.Now;
        foreach (var payment in payments)
        {
            payment.IsDeleted = true;
            payment.DeletedAt = now;
            payment.DeletedByUserId = User.GetUserId();
            payment.DeletedByUserName = User.Identity?.Name;
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, deletedCount = payments.Count });
    }

    [HttpPost("permanent-delete/{id:int}")]
    [HttpDelete("permanent-delete/{id:int}")]
    [HttpPost("/EmployeeSalaries/PermanentDeleteSalaryPayment")]
    public async Task<IActionResult> PermanentDeleteSalaryPayment([FromRoute] int id, CancellationToken ct)
    {
        var payment = await _context.EmployeeSalaryPayments
            .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted && !p.IsPermanentlyDeleted, ct);
        if (payment == null) return NotFound("Salary payment record not found.");

        payment.IsPermanentlyDeleted = true;
        payment.PermanentlyDeletedAt = IstanbulTimeHelper.Now;
        payment.PermanentlyDeletedByUserId = User.GetUserId();
        payment.PermanentlyDeletedByUserName = User.Identity?.Name;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("trash")]
    public async Task<IActionResult> Trash(CancellationToken ct)
    {
        var payments = await _context.EmployeeSalaryPayments.AsNoTracking()
            .Where(payment => payment.IsDeleted && !payment.IsPermanentlyDeleted)
            .OrderByDescending(payment => payment.DeletedAt)
            .Take(500)
            .Select(payment => new
            {
                payment.Id,
                payment.EmployeeId,
                payment.SalaryMonth,
                payment.RemainingAmount,
                payment.Currency,
                payment.DeletedAt,
                payment.DeletedByUserName
            })
            .ToListAsync(ct);
        return Ok(payments);
    }

    [HttpPost("restore/{id:int}")]
    public async Task<IActionResult> Restore([FromRoute] int id, CancellationToken ct)
    {
        var payment = await _context.EmployeeSalaryPayments
            .FirstOrDefaultAsync(item => item.Id == id && item.IsDeleted && !item.IsPermanentlyDeleted, ct);
        if (payment is null) return NotFound("Salary payment record not found in trash.");
        payment.IsDeleted = false;
        payment.DeletedAt = null;
        payment.DeletedByUserId = null;
        payment.DeletedByUserName = null;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    private ObjectResult NotImplemented(string detail) => StatusCode(
        StatusCodes.Status501NotImplemented,
        new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Operation not implemented",
            Detail = detail
        });
}

public record UpdateSalaryDueRequest(int EmployeeId, decimal Amount);
public record UpdateSalaryDaysRequest(int EmployeeId, int Days);
public record BulkSalaryConfirmRequest(List<int> EmployeeIds, DateTime? FromDate, DateTime? ToDate);
