using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Services;
using Luxira.Api.Features.Expenses.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant,DeliveryCompany,DeliveryRepresentative")]
[Route("api/v1/financial")]
[Route("Financial")]
public class FinancialController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly EmployeeService _employeeService;

    public FinancialController(ApplicationDbContext context, EmployeeService employeeService)
    {
        _context = context;
        _employeeService = employeeService;
    }

    [HttpGet("Index")]
    [HttpGet("/Financial/Index")]
    [HttpPost("/Financial/Index")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var balanceUpdated = await _context.Orders.AsNoTracking()
            .Where(order => order.OrderStatus == OrderStatusCodes.BalanceUpdated)
            .GroupBy(order => 1)
            .Select(group => new { Count = group.Count(), Total = group.Sum(order => order.TotalPrice), Delivery = group.Sum(order => order.DeliveryPrice) })
            .FirstOrDefaultAsync(ct);
        return Ok(new { balanceUpdated = balanceUpdated ?? new { Count = 0, Total = 0m, Delivery = 0m } });
    }

    [HttpGet("Countries")]
    [HttpGet("/Financial/Countries")]
    [HttpPost("/Financial/Countries")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> Countries(CancellationToken ct)
    {
        var orders = await _context.Orders
            .Where(o => o.OrderStatus == OrderStatusCodes.BalanceUpdated)
            .Select(o => new { o.Id, o.Country, o.TotalPrice, o.DeliveryPrice })
            .AsNoTracking()
            .ToListAsync(ct);

        var ordersByCountry = orders
            .GroupBy(o => o.Country)
            .Select(group => new
            {
                Country = group.Key,
                Count = group.Count(),
                TotalPrice = group.Sum(o => o.TotalPrice - o.DeliveryPrice),
                TotalGross = group.Sum(o => o.TotalPrice),
                TotalDelivery = group.Sum(o => o.DeliveryPrice)
            })
            .ToList();

        return Ok(ordersByCountry);
    }

    [HttpGet("OrderByManfacturingCompanyOnGoingDeliveryCompany")]
    [HttpGet("/Financial/OrderByManfacturingCompanyOnGoingDeliveryCompany")]
    [HttpPost("/Financial/OrderByManfacturingCompanyOnGoingDeliveryCompany")]
    public async Task<IActionResult> OrderByManfacturingCompanyOnGoingDeliveryCompany(
        [FromQuery] int? deliveryCompanyId,
        [FromQuery] int? countryId,
        [FromQuery] int? storeId,
        CancellationToken ct)
    {
        var query = _context.Orders
            .Include(o => o.DeliveryCompany)
            .Where(o => o.DeliveryCompany != null && o.DeliveryCompany.IsShown && !o.DeliveryCompany.IsRepresentative)
            .AsNoTracking()
            .AsQueryable();

        if (countryId.HasValue)
            query = query.Where(o => o.Country == countryId.Value);

        if (storeId.HasValue)
            query = query.Where(o => o.ManufacturingCompanyId == storeId.Value);

        if (deliveryCompanyId.HasValue)
            query = query.Where(o => o.DeliveryCompanyId == deliveryCompanyId.Value);

        // Filter by user if not admin/accountant
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector") || User.IsInRole("Accountant");
        var currentUserId = User.GetUserId();
        if (!isAdmin && !string.IsNullOrEmpty(currentUserId))
        {
            query = query.Where(o => o.DeliveryCompany != null && o.DeliveryCompany.UserId == currentUserId);
        }

        var orders = await query
            .Select(o => new
            {
                o.Id,
                o.ManufacturingCompanyId,
                o.DeliveryCompanyId,
                DeliveryCompanyName = o.DeliveryCompany != null ? o.DeliveryCompany.Name : string.Empty,
                o.OrderStatus,
                o.TotalPrice,
                o.DeliveryPrice,
                NetPrice = o.TotalPrice - o.DeliveryPrice,
                o.Country
            })
            .ToListAsync(ct);

        var grouped = orders
            .GroupBy(o => new { o.ManufacturingCompanyId, o.DeliveryCompanyId, o.DeliveryCompanyName })
            .Select(g => new
            {
                g.Key.ManufacturingCompanyId,
                g.Key.DeliveryCompanyId,
                g.Key.DeliveryCompanyName,
                OrderCount = g.Count(),
                TotalNetPrice = g.Sum(x => x.NetPrice),
                TotalGrossPrice = g.Sum(x => x.TotalPrice),
                TotalDeliveryFee = g.Sum(x => x.DeliveryPrice),
                BalanceUpdatedOrders = g.Count(
                    x => x.OrderStatus == OrderStatusCodes.BalanceUpdated)
            })
            .ToList();

        return Ok(grouped);
    }

    [HttpGet("OrderByManfacturingCompanyOnGoingDeliveryRepresntaitve")]
    [HttpGet("/Financial/OrderByManfacturingCompanyOnGoingDeliveryRepresntaitve")]
    [HttpPost("/Financial/OrderByManfacturingCompanyOnGoingDeliveryRepresntaitve")]
    public async Task<IActionResult> OrderByManfacturingCompanyOnGoingDeliveryRepresntaitve(
        [FromQuery] int? deliveryCompanyId,
        [FromQuery] int? countryId,
        [FromQuery] string? cityId,
        [FromQuery] int? storeId,
        CancellationToken ct)
    {
        var query = _context.Orders
            .Include(o => o.DeliveryCompany)
            .Where(o => o.DeliveryCompany != null && o.DeliveryCompany.IsShown && o.DeliveryCompany.IsRepresentative)
            .AsNoTracking()
            .AsQueryable();

        if (countryId.HasValue)
            query = query.Where(o => o.Country == countryId.Value);

        if (storeId.HasValue)
            query = query.Where(o => o.ManufacturingCompanyId == storeId.Value);

        if (deliveryCompanyId.HasValue)
            query = query.Where(o => o.DeliveryCompanyId == deliveryCompanyId.Value);

        var orders = await query
            .Select(o => new
            {
                o.Id,
                o.ManufacturingCompanyId,
                o.DeliveryCompanyId,
                RepresentativeName = o.DeliveryCompany != null ? o.DeliveryCompany.Name : string.Empty,
                o.OrderStatus,
                o.TotalPrice,
                o.DeliveryPrice,
                NetPrice = o.TotalPrice - o.DeliveryPrice
            })
            .ToListAsync(ct);

        return Ok(orders);
    }

    [HttpGet("OrderByManafactureCompanyThenDeliveryCompany")]
    [HttpGet("/Financial/OrderByManafactureCompanyThenDeliveryCompany")]
    [HttpPost("/Financial/OrderByManafactureCompanyThenDeliveryCompany")]
    public async Task<IActionResult> OrderByManafactureCompanyThenDeliveryCompany(
        [FromQuery] int? deliveryCompanyId,
        [FromQuery] int? manafacturecompanyId,
        CancellationToken ct)
    {
        var query = _context.Orders
            .Include(o => o.DeliveryCompany)
            .AsNoTracking()
            .AsQueryable();

        if (deliveryCompanyId.HasValue)
            query = query.Where(o => o.DeliveryCompanyId == deliveryCompanyId.Value);

        if (manafacturecompanyId.HasValue)
            query = query.Where(o => o.ManufacturingCompanyId == manafacturecompanyId.Value);

        var results = await query
            .GroupBy(o => new { o.ManufacturingCompanyId, o.DeliveryCompanyId })
            .Select(g => new
            {
                g.Key.ManufacturingCompanyId,
                g.Key.DeliveryCompanyId,
                TotalOrders = g.Count(),
                TotalSum = g.Sum(o => o.TotalPrice),
                TotalDelivery = g.Sum(o => o.DeliveryPrice)
            })
            .ToListAsync(ct);

        return Ok(results);
    }

    [HttpGet("Employees")]
    [HttpGet("/Financial/Employees")]
    [HttpPost("/Financial/Employees")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> Employees(
        [FromQuery] string? userId,
        [FromQuery] DateTime? startDay,
        [FromQuery] DateTime? endDay,
        CancellationToken ct)
    {
        var employeesQuery = _context.Employees
            .Where(e => e.IsActive)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            employeesQuery = employeesQuery.Where(e => e.ApplicationUserId == userId);
        }

        var employees = await employeesQuery.ToListAsync(ct);
        var now = IstanbulTimeHelper.Now;
        var sDate = startDay ?? new DateTime(now.Year, now.Month, 1);
        var eDate = endDay ?? sDate.AddMonths(1).AddDays(-1);

        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var applicationUserIds = employees.Select(employee => employee.ApplicationUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var transactionTotals = await _context.EmployeeTransactions.AsNoTracking()
            .Where(transaction => employeeIds.Contains(transaction.EmployeeId) && transaction.Date >= sDate && transaction.Date <= eDate)
            .GroupBy(transaction => transaction.EmployeeId)
            .Select(group => new
            {
                EmployeeId = group.Key,
                Deductions = group.Sum(item => item.TransactionType == EmployeeTransactionType.Deduction ? item.Amount : 0),
                Rewards = group.Sum(item => item.TransactionType == EmployeeTransactionType.Bonus ? item.Amount : 0),
                Advances = group.Sum(item => item.TransactionType == EmployeeTransactionType.Advance ? item.Amount : 0)
            }).ToDictionaryAsync(item => item.EmployeeId, ct);
        var bonusTotals = await _context.EmployeeBonusPayments.AsNoTracking()
            .Where(payment => applicationUserIds.Contains(payment.EmployeeId) && payment.DatePaid >= sDate && payment.DatePaid <= eDate)
            .GroupBy(payment => payment.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, Amount = group.Sum(item => item.AmountPaid) })
            .ToDictionaryAsync(item => item.EmployeeId, item => item.Amount, ct);

        var employeeSummaries = employees.Select(employee =>
        {
            transactionTotals.TryGetValue(employee.Id, out var totals);
            var bonuses = !string.IsNullOrWhiteSpace(employee.ApplicationUserId)
                ? bonusTotals.GetValueOrDefault(employee.ApplicationUserId) : 0m;
            var deductions = totals?.Deductions ?? 0m;
            var rewards = totals?.Rewards ?? 0m;
            var advances = totals?.Advances ?? 0m;
            return new
            {
                EmployeeId = employee.Id,
                EmployeeName = employee.Name,
                employee.JobTitle,
                BaseSalary = employee.Salary,
                TotalDeductions = deductions,
                TotalRewards = rewards,
                TotalAdvances = advances,
                TotalBonuses = bonuses,
                NetPayable = employee.Salary - deductions + rewards - advances + bonuses
            };
        }).ToList();

        return Ok(employeeSummaries);
    }

    [HttpPost("PayEmployee")]
    [HttpPost("/Financial/PayEmployee")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> PayEmployee([FromBody] PayEmployeeRequest request, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (employee == null)
            return NotFound("Employee not found.");

        var payment = await _employeeService.RecordSalaryPaymentAsync(
            new RecordSalaryPaymentRequest(
                employee.Id,
                request.Amount > 0 ? request.Amount : employee.Salary,
                request.Notes ?? "Monthly salary payout"),
            User.GetUserId() ?? "Admin",
            ct);

        return Ok(new { success = true, paymentId = payment.Id, amount = payment.Amount });
    }

    [HttpGet("EmployeeBonusDetails")]
    [HttpGet("/Financial/EmployeeBonusDetails")]
    [HttpPost("/Financial/EmployeeBonusDetails")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> EmployeeBonusDetails([FromQuery] int employeeId, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee == null)
            return NotFound("Employee not found.");

        var bonuses = await _context.EmployeeBonusPayments
            .Where(b => b.EmployeeId == employee.ApplicationUserId)
            .OrderByDescending(b => b.DatePaid)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(bonuses);
    }

    [HttpPost("MarkAsPaid")]
    [HttpPost("/Financial/MarkAsPaid")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> MarkAsPaid([FromBody] MarkAsPaidRequest request, CancellationToken ct)
    {
        var orders = await _context.Orders
            .Where(o => o.DeliveryCompanyId == request.DeliveryCompanyId &&
                        (!request.ManufactureCompanyId.HasValue || o.ManufacturingCompanyId == request.ManufactureCompanyId.Value) &&
                        o.OrderStatus == OrderStatusCodes.BalanceUpdated &&
                        !o.IsPaid)
            .ToListAsync(ct);

        if (orders.Count == 0)
            return Ok(new { success = false, message = "No unpaid orders found matching criteria." });

        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId() ?? "System";

        MarkOrdersAsPaid(
            orders,
            userId,
            now,
            "Marked as paid via Financial Settlement");

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, updatedCount = orders.Count, totalAmount = orders.Sum(o => o.TotalPrice) });
    }

    [HttpPost("MarkAsPaidAll")]
    [HttpPost("/Financial/MarkAsPaidAll")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> MarkAsPaidAll([FromBody] MarkAsPaidAllRequest request, CancellationToken ct)
    {
        var query = _context.Orders
            .Where(o => o.DeliveryCompanyId == request.DeliveryCompanyId &&
                        o.OrderStatus == OrderStatusCodes.BalanceUpdated &&
                        !o.IsPaid);

        if (request.ManufacturingCompanyIds != null && request.ManufacturingCompanyIds.Count > 0)
        {
            query = query.Where(o => o.ManufacturingCompanyId.HasValue && request.ManufacturingCompanyIds.Contains(o.ManufacturingCompanyId.Value));
        }

        var orders = await query.ToListAsync(ct);
        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId() ?? "System";

        MarkOrdersAsPaid(orders, userId, now, "Marked as paid in bulk");

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, updatedCount = orders.Count, totalAmount = orders.Sum(o => o.TotalPrice) });
    }

    [HttpPost("MarkAsPaidAllByCountry")]
    [HttpPost("/Financial/MarkAsPaidAllByCountry")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> MarkAsPaidAllByCountry([FromBody] MarkAsPaidCountryRequest request, CancellationToken ct)
    {
        var query = _context.Orders
            .Where(o => o.Country == request.CountryId &&
                        o.OrderStatus == OrderStatusCodes.BalanceUpdated &&
                        !o.IsPaid);

        if (request.DeliveryCompanyIds != null && request.DeliveryCompanyIds.Count > 0)
            query = query.Where(o => request.DeliveryCompanyIds.Contains(o.DeliveryCompanyId));

        if (request.ManufacturingCompanyIds != null && request.ManufacturingCompanyIds.Count > 0)
            query = query.Where(o => o.ManufacturingCompanyId.HasValue && request.ManufacturingCompanyIds.Contains(o.ManufacturingCompanyId.Value));

        var orders = await query.ToListAsync(ct);
        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId() ?? "System";

        MarkOrdersAsPaid(orders, userId, now, "Marked as paid by country");

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, updatedCount = orders.Count });
    }

    [HttpPost("MarkAsPaidAllByCity")]
    [HttpPost("/Financial/MarkAsPaidAllByCity")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> MarkAsPaidAllByCity([FromBody] MarkAsPaidCityRequest request, CancellationToken ct)
    {
        var query = _context.Orders
            .Where(o => o.Country == request.CountryId &&
                        o.OrderStatus == OrderStatusCodes.BalanceUpdated &&
                        !o.IsPaid);

        if (!string.IsNullOrEmpty(request.CityId))
            query = query.Where(o => o.State == request.CityId);

        var orders = await query.ToListAsync(ct);
        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId() ?? "System";

        MarkOrdersAsPaid(orders, userId, now, "Marked as paid by city");

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, updatedCount = orders.Count });
    }

    private void MarkOrdersAsPaid(
        IEnumerable<Order> orders,
        string userId,
        DateTime changedAt,
        string reason)
    {
        foreach (var order in orders)
        {
            var oldStatus = order.OrderStatus;
            order.IsPaid = true;
            order.LastEditedDate = changedAt;
            order.OrderStatus = OrderStatusCodes.Paid;

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatusCodes.Paid,
                ApplicationUserId = userId,
                CreatedAt = changedAt,
                Reason = reason,
                Name = $"PreviousStatus:{oldStatus}",
            });
        }
    }

    [HttpGet("OrderReports")]
    [HttpGet("/Financial/OrderReports")]
    public async Task<IActionResult> OrderReports(
        [FromQuery] int? storeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? deliveryCompanyIdFilter = null,
        [FromQuery] DateTime? startDay = null,
        [FromQuery] DateTime? endDay = null,
        [FromQuery] int? countryId = null,
        CancellationToken ct = default)
    {
        var query = _context.OrderReports
            .Include(r => r.ReportOrders)
            .AsNoTracking()
            .AsQueryable();

        var total = await query.CountAsync(ct);
        var reports = await query
            .OrderByDescending(r => r.GeneratedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new { r.Id, r.GeneratedTime, r.TotalAmount, r.Country, r.DeliveryCompanyId, r.OrderStatus, orderIds = r.ReportOrders.Select(item => item.OrderId) })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items = reports });
    }

    [HttpGet("OrderReportsRepresentative")]
    [HttpGet("/Financial/OrderReportsRepresentative")]
    public async Task<IActionResult> OrderReportsRepresentative(
        [FromQuery] int? storeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.OrderReports
            .Include(r => r.ReportOrders)
            .AsNoTracking()
            .AsQueryable();

        var reports = await query
            .OrderByDescending(r => r.GeneratedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new { r.Id, r.GeneratedTime, r.TotalAmount, r.Country, r.DeliveryCompanyId, r.OrderStatus, orderIds = r.ReportOrders.Select(item => item.OrderId) })
            .ToListAsync(ct);

        return Ok(reports);
    }

    [HttpGet("DownloadOrderReport/{orderReportId:int}")]
    [HttpGet("/Financial/DownloadOrderReport")]
    [HttpPost("/Financial/DownloadOrderReport")]
    public async Task<IActionResult> DownloadOrderReport([RouteOrRequest] int orderReportId, [FromQuery] int? id, CancellationToken ct)
    {
        var reportId = orderReportId > 0 ? orderReportId : (id ?? 0);
        var report = await _context.OrderReports
            .Include(r => r.ReportOrders)
            .ThenInclude(ro => ro.Order)
            .FirstOrDefaultAsync(r => r.Id == reportId, ct);

        if (report == null)
            return NotFound("Report not found.");

        return Ok(new
        {
            report.Id,
            report.GeneratedTime,
            report.TotalAmount,
            report.Country,
            report.DeliveryCompanyId,
            report.OrderStatus,
            Orders = report.ReportOrders.Select(ro => ro.Order)
        });
    }

    [HttpPost("GenerateCombinedReport")]
    [HttpPost("/Financial/GenerateCombinedReport")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
    public async Task<IActionResult> GenerateCombinedReport([FromBody] List<int> reportIds, CancellationToken ct)
    {
        var reports = await _context.OrderReports
            .Include(r => r.ReportOrders)
            .ThenInclude(ro => ro.Order)
            .Where(r => reportIds.Contains(r.Id))
            .ToListAsync(ct);

        var allOrders = reports.SelectMany(r => r.ReportOrders.Select(ro => ro.Order)).Where(o => o != null).ToList();

        return Ok(new
        {
            success = true,
            totalReports = reports.Count,
            totalOrders = allOrders.Count,
            totalAmount = allOrders.Sum(o => o?.TotalPrice ?? 0m)
        });
    }
}

public record PayEmployeeRequest(int EmployeeId, decimal Amount, string? Notes);
public record MarkAsPaidRequest(int DeliveryCompanyId, int? ManufactureCompanyId);
public record MarkAsPaidAllRequest(int DeliveryCompanyId, List<int>? ManufacturingCompanyIds);
public record MarkAsPaidCountryRequest(int CountryId, List<int>? DeliveryCompanyIds, List<int>? ManufacturingCompanyIds);
public record MarkAsPaidCityRequest(int CountryId, string? CityId);
