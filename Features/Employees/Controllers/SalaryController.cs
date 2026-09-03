using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Employees.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Infrastructure.Pdf;
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
    private readonly LuxiraPdfService _pdfService;

    public SalaryController(EmployeeService service, ApplicationDbContext context, LuxiraPdfService pdfService)
    {
        _service = service;
        _context = context;
        _pdfService = pdfService;
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
    public async Task<IActionResult> UpdateSalaryDueAmount([FromBody] UpdateSalaryDueRequest request, CancellationToken ct)
    {
        if (request.EmployeeId <= 0 || request.Amount < 0) throw new BadRequestException("Invalid employee or amount.");
        var payment = await GetOrCreateSalaryDraftAsync(request.EmployeeId, ct);
        if (payment.IsPaid) throw new BadRequestException("Paid salary rows cannot be edited.");
        payment.ManualAdjustmentAmount = request.Amount - payment.EarnedSalary + payment.TotalDeductions + payment.TotalAdvances - payment.TotalBonuses;
        payment.ManualAdjustmentReason = "Manual final salary override";
        payment.ManualAdjustmentAt = IstanbulTimeHelper.Now;
        payment.ManualAdjustmentByUserId = User.GetUserId();
        payment.ManualAdjustmentByUserName = User.Identity?.Name;
        payment.RemainingAmount = request.Amount;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, employeeId = request.EmployeeId, amount = payment.RemainingAmount, paymentId = payment.Id });
    }

    [HttpPost("update-days")]
    [HttpPost("/EmployeeSalaries/UpdateSalaryDays")]
    public async Task<IActionResult> UpdateSalaryDays([FromBody] UpdateSalaryDaysRequest request, CancellationToken ct)
    {
        var payment = await GetOrCreateSalaryDraftAsync(request.EmployeeId, ct);
        if (payment.IsPaid) throw new BadRequestException("Paid salary rows cannot be edited.");
        payment.DaysWorked = Math.Clamp(request.Days, 0, payment.DaysInMonth);
        payment.EarnedSalary = payment.DaysInMonth == 0 ? 0 : decimal.Round(payment.MonthlySalary * payment.DaysWorked / payment.DaysInMonth, 2);
        payment.RemainingAmount = payment.EarnedSalary - payment.TotalDeductions - payment.TotalAdvances + payment.TotalBonuses + payment.ManualAdjustmentAmount;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, employeeId = request.EmployeeId, daysWorked = payment.DaysWorked, amount = payment.RemainingAmount, paymentId = payment.Id });
    }

    [HttpPost("bulk-confirm")]
    [HttpPost("/EmployeeSalaries/BulkConfirmSalaryPayments")]
    public async Task<IActionResult> BulkConfirmSalaryPayments([FromBody] BulkSalaryConfirmRequest request, CancellationToken ct)
    {
        var ids = request.EmployeeIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0) throw new BadRequestException("No employees selected.");
        var drafts = await _context.EmployeeSalaryPayments.Where(payment => ids.Contains(payment.EmployeeId) && !payment.IsPaid && !payment.IsDeleted && !payment.IsPermanentlyDeleted).ToListAsync(ct);
        if (drafts.Select(payment => payment.EmployeeId).Distinct().Count() != ids.Length) throw new BadRequestException("Every employee must have a salary draft before bulk confirmation.");
        var now = IstanbulTimeHelper.Now;
        foreach (var payment in drafts)
        {
            payment.IsPaid = true; payment.PaidAt = now; payment.PaidByUserId = User.GetUserId(); payment.PaidByUserName = User.Identity?.Name;
            if (request.FromDate.HasValue) payment.PeriodFrom = request.FromDate.Value.Date;
            if (request.ToDate.HasValue) payment.PeriodTo = request.ToDate.Value.Date;
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, confirmedCount = drafts.Count });
    }

    [HttpPost("delete-payment/{paymentId:int}")]
    [HttpDelete("delete-payment/{paymentId:int}")]
    [HttpPost("/EmployeeSalaries/DeleteSalaryPayment")]
    public async Task<IActionResult> DeleteSalaryPayment([RouteOrRequest] int paymentId, [FromQuery] int? id, CancellationToken ct)
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
    public async Task<IActionResult> PermanentDeleteSalaryPayment([RouteOrRequest] int id, CancellationToken ct)
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
    [HttpPost("/EmployeeSalaries/RestoreSalaryPayment")]
    public async Task<IActionResult> Restore([RouteOrRequest] int id, CancellationToken ct)
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

    [HttpPost("/EmployeeSalaries/RestoreAllDeletedSalaryPayments")]
    public async Task<IActionResult> RestoreAllDeletedSalaryPayments(CancellationToken ct)
    {
        var rows = await _context.EmployeeSalaryPayments.Where(payment => payment.IsDeleted && !payment.IsPermanentlyDeleted).ToListAsync(ct);
        foreach (var payment in rows) { payment.IsDeleted = false; payment.DeletedAt = null; payment.DeletedByUserId = null; payment.DeletedByUserName = null; }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, restoredCount = rows.Count });
    }

    [HttpGet("/EmployeeSalaries/PermanentDeleteHistory")]
    public async Task<IActionResult> PermanentDeleteHistory(CancellationToken ct) => Ok(await _context.EmployeeSalaryPayments.AsNoTracking()
        .Where(payment => payment.IsPermanentlyDeleted).OrderByDescending(payment => payment.PermanentlyDeletedAt).Take(500)
        .Select(payment => new { payment.Id, payment.EmployeeId, payment.SalaryMonth, payment.RemainingAmount, payment.Currency, payment.PermanentlyDeletedAt, payment.PermanentlyDeletedByUserName }).ToListAsync(ct));

    [HttpGet("/EmployeeSalaries/SalaryReceiptPdf")]
    public async Task<IActionResult> SalaryReceiptPdf(int employeeId, DateTime? fromDate, DateTime? toDate, int? paymentId, CancellationToken ct)
    {
        var query = _context.EmployeeSalaryPayments.AsNoTracking().Include(payment => payment.Employee).Where(payment => payment.EmployeeId == employeeId && payment.IsPaid && !payment.IsDeleted);
        if (paymentId.HasValue) query = query.Where(payment => payment.Id == paymentId.Value);
        if (fromDate.HasValue) query = query.Where(payment => payment.PeriodTo >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(payment => payment.PeriodFrom <= toDate.Value.Date);
        var payment = await query.OrderByDescending(item => item.PaidAt).FirstOrDefaultAsync(ct);
        return payment is null ? NotFound() : File(_pdfService.GenerateSalaryPaymentReceiptPdf(payment), "application/pdf", $"salary-{payment.Id}.pdf");
    }

    [HttpGet("/EmployeeSalaries/FinancialStatementPdf")]
    public async Task<IActionResult> FinancialStatementPdf(int employeeId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        var query = _context.EmployeeTransactions.AsNoTracking().Include(transaction => transaction.Employee).Where(transaction => transaction.EmployeeId == employeeId);
        if (fromDate.HasValue) query = query.Where(transaction => transaction.Date >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(transaction => transaction.Date < toDate.Value.Date.AddDays(1));
        return File(_pdfService.GenerateEmployeeTransactionsStatementPdf(await query.OrderBy(transaction => transaction.Date).ToListAsync(ct)), "application/pdf", $"financial-statement-{employeeId}.pdf");
    }

    [HttpGet("/EmployeeSalaries/SalaryStatementPdf")]
    public async Task<IActionResult> SalaryStatementPdf(int employeeId, CancellationToken ct)
    {
        var rows = await _context.EmployeeSalaryPayments.AsNoTracking().Include(payment => payment.Employee).Where(payment => payment.EmployeeId == employeeId && !payment.IsDeleted)
            .OrderBy(payment => payment.SalaryMonth).ToListAsync(ct);
        return File(_pdfService.GenerateSalaryStatementPdf(rows), "application/pdf", $"salary-statement-{employeeId}.pdf");
    }

    [HttpGet("/EmployeeSalaries/TransferArchive")]
    public async Task<IActionResult> TransferArchive(int? employeeId, CancellationToken ct)
    {
        var query = _context.EmployeeSalaryPayments.AsNoTracking().Where(payment => payment.IsPaid && !payment.IsDeleted);
        if (employeeId.HasValue) query = query.Where(payment => payment.EmployeeId == employeeId.Value);
        return Ok(await query.OrderByDescending(payment => payment.PaidAt).Take(500).ToListAsync(ct));
    }

    [HttpGet("/EmployeeSalaries/TestSalaryNotification")]
    [HttpPost("/EmployeeSalaries/TestSalaryNotification")]
    public async Task<IActionResult> TestSalaryNotification(int? employeeId, CancellationToken ct)
    {
        var count = await _context.EmployeeSalaryPayments.AsNoTracking().CountAsync(payment => !payment.IsPaid && !payment.IsDeleted && (!employeeId.HasValue || payment.EmployeeId == employeeId), ct);
        return Ok(new { success = true, pendingSalaryCount = count, message = "Salary notification query completed." });
    }

    private async Task<EmployeeSalaryPayment> GetOrCreateSalaryDraftAsync(int employeeId, CancellationToken ct)
    {
        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employeeId, ct)
            ?? throw new NotFoundException("Employee not found.");
        var now = IstanbulTimeHelper.Now;
        var month = new DateTime(now.Year, now.Month, 1);
        var payment = await _context.EmployeeSalaryPayments.FirstOrDefaultAsync(item => item.EmployeeId == employeeId && item.SalaryMonth == month && !item.IsPermanentlyDeleted, ct);
        if (payment is not null) return payment;
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        payment = new EmployeeSalaryPayment
        {
            EmployeeId = employeeId, SalaryMonth = month, PeriodFrom = month, PeriodTo = now.Date,
            MonthlySalary = employee.Salary, DaysWorked = Math.Min(now.Day, days), DaysInMonth = days,
            EarnedSalary = decimal.Round(employee.Salary * Math.Min(now.Day, days) / days, 2), Currency = "USD",
            ReceiptNumber = $"SAL-{employeeId}-{now:yyyyMM}-{Guid.NewGuid():N}"[..Math.Min(80, $"SAL-{employeeId}-{now:yyyyMM}-{Guid.NewGuid():N}".Length)]
        };
        payment.RemainingAmount = payment.EarnedSalary;
        _context.EmployeeSalaryPayments.Add(payment);
        return payment;
    }

}

public record UpdateSalaryDueRequest(int EmployeeId, decimal Amount);
public record UpdateSalaryDaysRequest(int EmployeeId, int Days);
public record BulkSalaryConfirmRequest(List<int> EmployeeIds, DateTime? FromDate, DateTime? ToDate);
