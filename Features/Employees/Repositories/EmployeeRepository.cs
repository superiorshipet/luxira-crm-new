using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;

namespace Luxira.Api.Features.Employees.Repositories;

public class EmployeeRepository
{
    private readonly ApplicationDbContext _context;

    public EmployeeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Employee>> GetAllAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.Employees.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
        {
            query = query.Where(e => e.IsActive == isActive.Value);
        }
        return await query.OrderBy(e => e.Name).ToListAsync(ct);
    }

    public async Task<Employee?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Employees
            .Include(e => e.AttendanceLogs)
            .Include(e => e.SalaryPayments)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<Employee?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == userId, ct);
    }

    public async Task<Employee> AddAsync(Employee employee, CancellationToken ct = default)
    {
        var result = await _context.Employees.AddAsync(employee, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task UpdateAsync(Employee employee, CancellationToken ct = default)
    {
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<EmployeeAttendanceLog> AddAttendanceLogAsync(EmployeeAttendanceLog log, CancellationToken ct = default)
    {
        var result = await _context.EmployeeAttendanceLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<EmployeeAttendanceLog?> GetAttendanceLogByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.EmployeeAttendanceLogs.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task UpdateAttendanceLogAsync(EmployeeAttendanceLog log, CancellationToken ct = default)
    {
        _context.EmployeeAttendanceLogs.Update(log);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<EmployeeAttendanceLog>> GetAttendanceLogsAsync(int? employeeId = null, DateTime? date = null, CancellationToken ct = default)
    {
        var query = _context.EmployeeAttendanceLogs.Include(a => a.Employee).AsNoTracking().AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        }

        if (date.HasValue)
        {
            var dayStart = date.Value.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(a => a.CheckInAt >= dayStart && a.CheckInAt < dayEnd);
        }

        return await query.OrderByDescending(a => a.CheckInAt).ToListAsync(ct);
    }

    public async Task<EmployeeSalaryPayment> AddSalaryPaymentAsync(EmployeeSalaryPayment payment, CancellationToken ct = default)
    {
        var result = await _context.EmployeeSalaryPayments.AddAsync(payment, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<List<EmployeeSalaryPayment>> GetSalaryPaymentsAsync(int? employeeId = null, CancellationToken ct = default)
    {
        var query = _context.EmployeeSalaryPayments.Include(s => s.Employee).AsNoTracking().AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(s => s.EmployeeId == employeeId.Value);
        }

        return await query.OrderByDescending(s => s.PaymentDate).ToListAsync(ct);
    }
}
