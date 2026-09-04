using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator")]
[Route("BonusEmployeeTest")]
public sealed class BonusEmployeeTestController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? employeeId, CancellationToken ct)
    {
        var employees = await context.Employees.AsNoTracking().Where(employee => employee.IsActive && employee.ApplicationUserId != null)
            .OrderBy(employee => employee.DisplayName ?? employee.Name)
            .Select(employee => new { employee.ApplicationUserId, Name = employee.DisplayName ?? employee.Name }).ToListAsync(ct);
        return Ok(new { employeeId, employees, now = DateTimeOffset.UtcNow });
    }

    [HttpGet("RunPartial")]
    public async Task<IActionResult> RunPartial([FromQuery] string? employeeId, [FromQuery] bool onlyUnpaid = true, [FromQuery] int rowCap = 400, CancellationToken ct = default)
    {
        rowCap = Math.Clamp(rowCap, 1, 2000);
        var rates = context.EmployeeBonusRates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(employeeId)) rates = rates.Where(rate => rate.EmployeeId == employeeId);
        var data = await rates.OrderBy(rate => rate.EmployeeId).Take(rowCap).Select(rate => new
        {
            rate.EmployeeId, rate.BonusPercentage, rate.BonusProcessingPercentage, rate.ProBonusPercentage,
            rate.ProBonusProcessingPercentage, rate.ProThreshold, rate.MinimumBonusThreshold
        }).ToListAsync(ct);
        var ids = data.Select(item => item.EmployeeId).ToList();
        var paymentsQuery = context.EmployeeBonusPayments.AsNoTracking().Where(payment => ids.Contains(payment.EmployeeId));
        var payments = await paymentsQuery.GroupBy(payment => payment.EmployeeId).Select(group => new { EmployeeId = group.Key, Paid = group.Sum(payment => payment.AmountPaid), Count = group.Count() }).ToListAsync(ct);
        return Ok(new { onlyUnpaid, rows = data, payments });
    }

    [HttpGet("CutoffPartial")]
    public async Task<IActionResult> CutoffPartial([FromQuery] string? employeeId, CancellationToken ct)
    {
        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var query = context.EmployeeBonusPayments.AsNoTracking().Where(payment => payment.DatePaid < currentMonth);
        if (!string.IsNullOrWhiteSpace(employeeId)) query = query.Where(payment => payment.EmployeeId == employeeId);
        var closed = await query.GroupBy(payment => payment.EmployeeId).Select(group => new { EmployeeId = group.Key, Amount = group.Sum(payment => payment.AmountPaid), Payments = group.Count() }).ToListAsync(ct);
        return Ok(new { currentCycleStart = currentMonth, closedCyclePayments = closed });
    }

    [HttpGet("OrderPartial")]
    public async Task<IActionResult> OrderPartial([FromQuery] int orderId, [FromQuery] bool onlyUnpaid = true, CancellationToken ct = default)
    {
        var order = await context.Orders.AsNoTracking().Where(item => item.Id == orderId).Select(item => new
        {
            item.Id, item.ApplicationUserId, item.Editedby, item.Fixedby, item.TotalPrice, item.DeliveryPrice,
            Profit = item.TotalPrice - item.DeliveryPrice, item.IsBonus, item.IsBonusPaidForEmployee, item.BonusPaymentId,
            item.OrderStatus, item.CreatedDate, item.LastEditedDate
        }).FirstOrDefaultAsync(ct);
        if (order is null) return NotFound();
        if (onlyUnpaid && order.IsBonusPaidForEmployee) return Ok(new { order, eligible = false, reason = "Bonus already paid." });
        var employeeIds = new[] { order.ApplicationUserId, order.Editedby, order.Fixedby }.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var rates = await context.EmployeeBonusRates.AsNoTracking().Where(rate => employeeIds.Contains(rate.EmployeeId)).ToListAsync(ct);
        return Ok(new { order, eligible = order.OrderStatus is OrderStatusCodes.BalanceUpdated or OrderStatusCodes.Paid, rates });
    }
}
