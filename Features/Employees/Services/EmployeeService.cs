using Luxira.Api.Data;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Employees.Repositories;
using Luxira.Api.Utils.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Services;

public class EmployeeService
{
    private readonly EmployeeRepository _repository;
    private readonly ApplicationDbContext _context;

    public EmployeeService(EmployeeRepository repository, ApplicationDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var list = await _repository.GetAllAsync(isActive, ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDto> GetEmployeeByIdAsync(int id, CancellationToken ct = default)
    {
        var employee = await _repository.GetByIdAsync(id, ct);
        if (employee == null)
        {
            throw new NotFoundException($"Employee with ID {id} not found.");
        }
        return MapToDto(employee);
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Employee name is required.");
        }

        var entity = new Employee
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            IdNumber = request.IdNumber,
            Nationality = request.Nationality,
            Country = request.Country,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            Salary = request.Salary,
            JobTitle = request.JobTitle,
            ApplicationUserId = request.ApplicationUserId,
            HireDate = DateTime.UtcNow,
            IsActive = true
        };

        var created = await _repository.AddAsync(entity, ct);
        return MapToDto(created);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(int id, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _repository.GetByIdAsync(id, ct);
        if (employee == null)
        {
            throw new NotFoundException($"Employee with ID {id} not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name)) employee.Name = request.Name;
        if (request.DisplayName != null) employee.DisplayName = request.DisplayName;
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber)) employee.PhoneNumber = request.PhoneNumber;
        if (!string.IsNullOrWhiteSpace(request.Address)) employee.Address = request.Address;
        if (request.Salary.HasValue) employee.Salary = request.Salary.Value;
        if (request.JobTitle != null) employee.JobTitle = request.JobTitle;
        if (request.IsActive.HasValue) employee.IsActive = request.IsActive.Value;

        await _repository.UpdateAsync(employee, ct);
        return MapToDto(employee);
    }

    public async Task<AttendanceLogDto> CheckInAsync(CheckInRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var employee = await _repository.GetByIdAsync(request.EmployeeId, ct);
        if (employee == null)
        {
            throw new NotFoundException($"Employee with ID {request.EmployeeId} not found.");
        }

        var log = new EmployeeAttendanceLog
        {
            EmployeeId = request.EmployeeId,
            CheckIn = DateTime.UtcNow,
            IpAddress = ipAddress,
            Note = request.Note
        };

        var created = await _repository.AddAttendanceLogAsync(log, ct);
        return new AttendanceLogDto(created.Id, created.EmployeeId, employee.Name, created.CheckIn, created.CheckOut, created.Note);
    }

    public async Task<AttendanceLogDto> CheckOutAsync(CheckOutRequest request, CancellationToken ct = default)
    {
        var log = await _repository.GetAttendanceLogByIdAsync(request.AttendanceLogId, ct);
        if (log == null)
        {
            throw new NotFoundException($"Attendance log with ID {request.AttendanceLogId} not found.");
        }

        log.CheckOut = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            log.Note = string.IsNullOrEmpty(log.Note) ? request.Note : $"{log.Note} | {request.Note}";
        }

        await _repository.UpdateAttendanceLogAsync(log, ct);
        var employee = await _repository.GetByIdAsync(log.EmployeeId, ct);
        return new AttendanceLogDto(log.Id, log.EmployeeId, employee?.Name ?? string.Empty, log.CheckIn, log.CheckOut, log.Note);
    }

    public async Task<List<AttendanceLogDto>> GetAttendanceLogsAsync(int? employeeId = null, DateTime? date = null, CancellationToken ct = default)
    {
        var logs = await _repository.GetAttendanceLogsAsync(employeeId, date, ct);
        return logs.Select(l => new AttendanceLogDto(
            l.Id,
            l.EmployeeId,
            l.Employee?.Name ?? string.Empty,
            l.CheckIn,
            l.CheckOut,
            l.Note
        )).ToList();
    }

    public async Task<SalaryPaymentDto> RecordSalaryPaymentAsync(RecordSalaryPaymentRequest request, string userId, CancellationToken ct = default)
    {
        var employee = await _repository.GetByIdAsync(request.EmployeeId, ct);
        if (employee == null)
        {
            throw new NotFoundException($"Employee with ID {request.EmployeeId} not found.");
        }

        var payment = new EmployeeSalaryPayment
        {
            EmployeeId = request.EmployeeId,
            Amount = request.Amount,
            Notes = request.Notes,
            PaidByUserId = userId,
            PaymentDate = DateTime.UtcNow
        };

        var created = await _repository.AddSalaryPaymentAsync(payment, ct);
        return new SalaryPaymentDto(created.Id, created.EmployeeId, employee.Name, created.Amount, created.PaymentDate, created.Notes);
    }

    public async Task<List<SalaryPaymentDto>> GetSalaryPaymentsAsync(int? employeeId = null, CancellationToken ct = default)
    {
        var payments = await _repository.GetSalaryPaymentsAsync(employeeId, ct);
        return payments.Select(p => new SalaryPaymentDto(
            p.Id,
            p.EmployeeId,
            p.Employee?.Name ?? string.Empty,
            p.Amount,
            p.PaymentDate,
            p.Notes
        )).ToList();
    }

    public async Task<List<PayrollSummaryDto>> CalculatePayrollAsync(int? employeeId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        int totalDays = DateTime.DaysInMonth(year, month);

        var empQuery = _context.Employees.AsNoTracking().Where(e => e.IsActive);
        if (employeeId.HasValue && employeeId.Value > 0)
        {
            empQuery = empQuery.Where(e => e.Id == employeeId.Value);
        }

        var employees = await empQuery.ToListAsync(ct);
        var summaries = new List<PayrollSummaryDto>();

        foreach (var emp in employees)
        {
            var attendedDays = await _context.EmployeeAttendanceLogs
                .Where(a => a.EmployeeId == emp.Id && a.CheckIn >= startDate && a.CheckIn < endDate)
                .Select(a => a.CheckIn.Date)
                .Distinct()
                .CountAsync(ct);

            // If no biometric logs recorded yet, treat active employees as fully attended for preview
            int effectiveDays = attendedDays > 0 ? attendedDays : totalDays;
            decimal dailyRate = totalDays > 0 ? emp.Salary / totalDays : 0;
            decimal earnedSalary = Math.Round(dailyRate * effectiveDays, 2);

            decimal bonuses = await _context.EmployeeBonusPayments
                .Where(b => b.EmployeeId == emp.Id && b.Date >= startDate && b.Date < endDate)
                .SumAsync(b => (decimal?)b.Amount, ct) ?? 0;

            decimal deductions = await _context.EmployeeViolations
                .Where(v => v.EmployeeId == emp.Id && v.OccurredAt >= startDate && v.OccurredAt < endDate)
                .SumAsync(v => (decimal?)v.PenaltyAmount, ct) ?? 0;

            decimal advances = await _context.EmployeeTransactions
                .Where(t => t.EmployeeId == emp.Id && t.Date >= startDate && t.Date < endDate && t.TransactionType == "Advance")
                .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

            decimal paidAmount = await _context.EmployeeSalaryPayments
                .Where(p => p.EmployeeId == emp.Id && p.PaymentDate >= startDate && p.PaymentDate < endDate)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

            decimal netDue = Math.Max(0, earnedSalary + bonuses - deductions - advances);
            decimal remaining = Math.Max(0, netDue - paidAmount);

            summaries.Add(new PayrollSummaryDto(
                emp.Id,
                emp.Name,
                emp.Salary,
                effectiveDays,
                totalDays,
                earnedSalary,
                bonuses,
                deductions,
                advances,
                paidAmount,
                netDue,
                remaining,
                year,
                month
            ));
        }

        return summaries;
    }

    private static EmployeeDto MapToDto(Employee e) => new(
        e.Id,
        e.Name,
        e.DisplayName,
        e.IdNumber,
        e.Nationality,
        e.Country,
        e.PhoneNumber,
        e.Address,
        e.Salary,
        e.JobTitle,
        e.HireDate,
        e.IsActive,
        e.ImageUrl,
        e.ApplicationUserId
    );
}

public record PayrollSummaryDto(
    int EmployeeId,
    string EmployeeName,
    decimal BaseSalary,
    int AttendedDays,
    int TotalDaysInMonth,
    decimal EarnedSalary,
    decimal TotalBonuses,
    decimal TotalDeductions,
    decimal TotalAdvances,
    decimal PaidAmount,
    decimal NetSalaryDue,
    decimal RemainingDue,
    int Year,
    int Month
);
