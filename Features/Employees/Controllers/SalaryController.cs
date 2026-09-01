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
        return Ok(new { success = true, employeeId = request.EmployeeId, newDueAmount = request.Amount });
    }

    [HttpPost("update-days")]
    [HttpPost("/EmployeeSalaries/UpdateSalaryDays")]
    public IActionResult UpdateSalaryDays([FromBody] UpdateSalaryDaysRequest request)
    {
        return Ok(new { success = true, employeeId = request.EmployeeId, days = request.Days });
    }

    [HttpPost("bulk-confirm")]
    [HttpPost("/EmployeeSalaries/BulkConfirmSalaryPayments")]
    public async Task<IActionResult> BulkConfirmSalaryPayments([FromBody] BulkSalaryConfirmRequest request, CancellationToken ct)
    {
        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId() ?? "Admin";

        foreach (var empId in request.EmployeeIds)
        {
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == empId, ct);
            if (emp != null)
            {
                var payment = new EmployeeSalaryPayment
                {
                    EmployeeId = emp.Id,
                    Amount = emp.Salary,
                    PaymentDate = now,
                    PaidByUserId = userId,
                    Notes = "Bulk Confirm Payroll"
                };
                await _context.EmployeeSalaryPayments.AddAsync(payment, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, confirmedCount = request.EmployeeIds.Count });
    }

    [HttpPost("delete-payment/{paymentId:int}")]
    [HttpDelete("delete-payment/{paymentId:int}")]
    [HttpPost("/EmployeeSalaries/DeleteSalaryPayment")]
    public async Task<IActionResult> DeleteSalaryPayment([FromRoute] int paymentId, [FromQuery] int? id, CancellationToken ct)
    {
        var targetId = paymentId > 0 ? paymentId : (id ?? 0);
        var payment = await _context.EmployeeSalaryPayments.FirstOrDefaultAsync(p => p.Id == targetId, ct);
        if (payment == null) return NotFound("Salary payment record not found.");

        _context.EmployeeSalaryPayments.Remove(payment);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, deletedId = targetId });
    }

    [HttpPost("bulk-delete")]
    [HttpPost("/EmployeeSalaries/BulkDeleteSalaryPayments")]
    public async Task<IActionResult> BulkDeleteSalaryPayments([FromBody] List<int> paymentIds, CancellationToken ct)
    {
        var payments = await _context.EmployeeSalaryPayments.Where(p => paymentIds.Contains(p.Id)).ToListAsync(ct);
        _context.EmployeeSalaryPayments.RemoveRange(payments);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, deletedCount = payments.Count });
    }

    [HttpPost("permanent-delete/{id:int}")]
    [HttpDelete("permanent-delete/{id:int}")]
    [HttpPost("/EmployeeSalaries/PermanentDeleteSalaryPayment")]
    public async Task<IActionResult> PermanentDeleteSalaryPayment([FromRoute] int id, CancellationToken ct)
    {
        var payment = await _context.EmployeeSalaryPayments.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment == null) return NotFound("Salary payment record not found.");

        _context.EmployeeSalaryPayments.Remove(payment);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public record UpdateSalaryDueRequest(int EmployeeId, decimal Amount);
public record UpdateSalaryDaysRequest(int EmployeeId, int Days);
public record BulkSalaryConfirmRequest(List<int> EmployeeIds, DateTime? FromDate, DateTime? ToDate);
