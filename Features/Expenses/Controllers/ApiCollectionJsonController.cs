using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Route("ApiCollectionJson")]
public sealed class ApiCollectionJsonController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("Index")]
    [HttpPost("Index")]
    public IActionResult Index() => Ok(new { });

    [HttpGet("OrderByCountry")]
    [HttpPost("OrderByCountry")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> OrderByCountry([FromQuery] int? countryId, CancellationToken ct)
    {
        var query = context.Orders.AsNoTracking().Where(order => order.OrderStatus == OrderStatusCodes.BalanceUpdated);
        if (countryId.HasValue) query = query.Where(order => order.Country == countryId.Value);
        var rows = await query.GroupBy(order => order.Country)
            .Select(group => new { SelectedCountry = group.Key, TotalPrice = group.Sum(order => order.TotalPrice) })
            .ToListAsync(ct);
        var rates = await context.ExchangeRates.AsNoTracking().ToDictionaryAsync(rate => rate.Country, ct);
        return Ok(rows.Select(row => new
        {
            row.SelectedCountry,
            Currency = CurrencyForCountry(row.SelectedCountry),
            TotalPrice = decimal.Round(row.TotalPrice, 2),
            TotalPirceDollar = rates.TryGetValue(row.SelectedCountry, out var rate) && rate.SellToUSD > 0
                ? decimal.Round(row.TotalPrice / rate.SellToUSD, 2) : row.TotalPrice
        }));
    }

    [HttpGet("GetOrderReports")]
    [Authorize(Roles = "Admin,Administrator,DeliveryRepresentative,DeliveryCompany")]
    public async Task<IActionResult> GetOrderReports([FromQuery] int? countryId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Administrator");
        var query = from report in context.OrderReports.AsNoTracking()
                    join company in context.DeliveryCompanies.AsNoTracking() on report.DeliveryCompanyId equals company.Id
                    where report.DeliveryCompanyId != null && (isAdmin || company.UserId == userId)
                    select new { report, company };
        if (countryId.HasValue) query = query.Where(item => item.report.Country == countryId.Value);
        return Ok(await query.OrderByDescending(item => item.report.GeneratedTime).Take(10).Select(item => new
        {
            id = item.report.Id.ToString(),
            generatedTime = item.report.GeneratedTime.ToString("yyyy-MM-dd"),
            totalAmount = item.report.TotalAmount,
            country = item.report.Country,
            deliveryCompanyName = item.company.Name
        }).ToListAsync(ct));
    }

    [HttpGet("PaidAccountsDeliveryCompany")]
    [HttpPost("PaidAccountsDeliveryCompany")]
    [Authorize(Roles = "DeliveryRepresentative,DeliveryCompany")]
    public Task<IActionResult> PaidAccountsDeliveryCompany(CancellationToken ct) => DeliveryAccounts(true, ct);

    [HttpGet("OnGoingAccountDeliveryCompany")]
    [HttpPost("OnGoingAccountDeliveryCompany")]
    [Authorize(Roles = "DeliveryRepresentative,DeliveryCompany")]
    public Task<IActionResult> OnGoingAccountDeliveryCompany(CancellationToken ct) => DeliveryAccounts(false, ct);

    [HttpGet("ExistingsForEmployess")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Authorize(Roles = "CallCenter,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> ExistingsForEmployess(CancellationToken ct)
    {
        var employee = await CurrentEmployee().FirstOrDefaultAsync(ct);
        if (employee is null) return Ok(EmptyReceivedSalary());
        var paid = await context.EmployeeSalaryPayments.AsNoTracking()
            .Where(payment => payment.EmployeeId == employee.Id && (payment.IsPaid || payment.PaidAt.HasValue) && !payment.IsDeleted && !payment.IsPermanentlyDeleted)
            .ToListAsync(ct);
        var rows = paid.GroupBy(payment => new { payment.SalaryMonth.Year, payment.SalaryMonth.Month })
            .Select(group => group.OrderByDescending(payment => payment.PaidAt).ThenByDescending(payment => payment.Id).First())
            .OrderBy(payment => payment.SalaryMonth).ToList();
        var currency = CurrencyForEmployee(employee);
        var payments = new List<EmployeeSalaryReceivedRow>();
        var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var firstRecordedMonth = rows.Count == 0 ? currentMonth : new DateTime(rows[0].SalaryMonth.Year, rows[0].SalaryMonth.Month, 1);
        for (var month = new DateTime(employee.DateAdded.Year, employee.DateAdded.Month, 1); month < firstRecordedMonth && month < currentMonth; month = month.AddMonths(1))
            payments.Add(new(month.ToString("MM/yyyy"), decimal.Round(employee.Salary, 2), currency, true));
        payments.AddRange(rows.Select(payment => new EmployeeSalaryReceivedRow(payment.SalaryMonth.ToString("MM/yyyy"), decimal.Round(payment.RemainingAmount, 2), string.IsNullOrWhiteSpace(payment.Currency) ? currency : payment.Currency, false)));
        var total = payments.Sum(payment => payment.Amount);
        var countryText = string.IsNullOrWhiteSpace(employee.Country) ? employee.Nationality : employee.Country;
        var countryId = CountryId(countryText);
        var sellRate = countryId.HasValue ? await context.ExchangeRates.AsNoTracking().Where(rate => rate.Country == countryId).Select(rate => (decimal?)rate.SellToUSD).FirstOrDefaultAsync(ct) : null;
        var totalUsd = sellRate > 0 ? decimal.Round(total / sellRate.Value, 2) : total;
        return Ok(new { TotalEarned = total.ToString("0.00"), TotalEarnedUSD = totalUsd.ToString("0.00"), TotalReceived = total, Currency = currency, Payments = payments });
    }

    [HttpGet("Last10EmployeeTransactions")]
    [Authorize(Roles = "CallCenter,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> Last10EmployeeTransactions(CancellationToken ct)
    {
        var employee = await CurrentEmployee().Select(item => new { item.Id }).FirstOrDefaultAsync(ct);
        if (employee is null) return Ok(new { Message = "Employee not found." });
        var rows = await context.EmployeeTransactions.AsNoTracking().Where(item => item.EmployeeId == employee.Id && !item.IsDeleted)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id).Take(10).ToListAsync(ct);
        return Ok(rows.Select(item => new { item.Id, item.Amount, TransactionType = TransactionTypeText(item.TransactionType), TransactionTypeValue = (int)item.TransactionType, item.Reason, Date = item.Date.ToString("yyyy-MM-dd"), IsPositive = IsPositive(item.TransactionType), ColorClass = IsPositive(item.TransactionType) ? "is-positive" : "is-deduction", TextColor = IsPositive(item.TransactionType) ? "#159447" : "#dc2626" }));
    }

    [HttpGet("CalculateAdjustedSalary")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Authorize(Roles = "CallCenter,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> CalculateAdjustedSalary(CancellationToken ct)
    {
        var employee = await CurrentEmployee().FirstOrDefaultAsync(ct);
        if (employee is null) return Ok(new { Message = "Employee not found." });
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var periodStart = employee.DateAdded.Date > monthStart ? employee.DateAdded.Date : monthStart;
        var days = Math.Max(1, (now.Date - periodStart).Days + 1);
        var earned = decimal.Round(employee.Salary * days / DateTime.DaysInMonth(now.Year, now.Month), 2);
        var transactions = await context.EmployeeTransactions.AsNoTracking().Where(item => item.EmployeeId == employee.Id && !item.IsDeleted && item.Date >= periodStart && item.Date <= now).ToListAsync(ct);
        var deductions = transactions.Where(item => item.TransactionType == EmployeeTransactionType.Deduction).Sum(item => Math.Abs(item.Amount));
        var advances = transactions.Where(item => item.TransactionType == EmployeeTransactionType.Advance).Sum(item => Math.Abs(item.Amount));
        var bonuses = transactions.Where(item => item.TransactionType is EmployeeTransactionType.Bonus or EmployeeTransactionType.Overtime).Sum(item => Math.Abs(item.Amount));
        var overtime = transactions.Where(item => item.TransactionType == EmployeeTransactionType.Overtime).Sum(item => Math.Abs(item.Amount));
        var countryText = string.IsNullOrWhiteSpace(employee.Country) ? employee.Nationality : employee.Country;
        var commissionUsd = string.IsNullOrWhiteSpace(employee.ApplicationUserId) ? 0m : await CalculateUnpaidCommissionUsd(employee.ApplicationUserId, countryText, now, ct);
        var countryId = CountryId(countryText); var employeeRate = countryId.HasValue ? await context.ExchangeRates.AsNoTracking().Where(item => item.Country == countryId).Select(item => (decimal?)item.SellToUSD).FirstOrDefaultAsync(ct) : null;
        var commission = employeeRate > 0 ? decimal.Round(commissionUsd * employeeRate.Value, 2, MidpointRounding.AwayFromZero) : decimal.Round(commissionUsd, 2, MidpointRounding.AwayFromZero);
        var due = Math.Max(0, decimal.Round(earned + bonuses + commission - deductions - advances, 2, MidpointRounding.AwayFromZero));
        var manual = await context.EmployeeSalaryPayments.AsNoTracking().Where(payment => payment.EmployeeId == employee.Id && !payment.IsPaid && !payment.IsDeleted && !payment.IsPermanentlyDeleted && payment.SalaryMonth.Year == now.Year && payment.SalaryMonth.Month == now.Month && payment.ReceiptNumber != null)
            .OrderByDescending(payment => payment.Id).Select(payment => new { payment.RemainingAmount, payment.ReceiptNumber }).FirstOrDefaultAsync(ct);
        var manualApplied = manual is not null && (manual.ReceiptNumber.StartsWith("MANUAL|", StringComparison.OrdinalIgnoreCase) || manual.ReceiptNumber.StartsWith("MANUALAMOUNT|", StringComparison.OrdinalIgnoreCase) || manual.ReceiptNumber.StartsWith("MANUALBOTH|", StringComparison.OrdinalIgnoreCase));
        if (manualApplied) due = Math.Max(0, decimal.Round(manual!.RemainingAmount, 2, MidpointRounding.AwayFromZero));
        var dueUsd = employeeRate > 0 ? decimal.Round(due / employeeRate.Value, 2, MidpointRounding.AwayFromZero) : due;
        return Ok(new { AdjustedSalary = due.ToString("0.00"), Transactions = transactions.Select(item => new { item.Id, item.Amount, TransactionType = TransactionTypeText(item.TransactionType), TransactionTypeValue = (int)item.TransactionType, item.Reason, Date = item.Date.ToString("yyyy-MM-dd"), IsPositive = IsPositive(item.TransactionType) }), AdjustedSalaryUSD = dueUsd.ToString("0.00"), TotalSalary = earned.ToString("0.00"), TotalBonus = bonuses.ToString("0.00"), TotalOvertime = overtime.ToString("0.00"), TotalAdvance = advances.ToString("0.00"), TotalDeduction = deductions.ToString("0.00"), TotalOngoingAccount = due.ToString("0.00"), CommissionAmount = commission, AccruedDays = days, DaysInMonth = DateTime.DaysInMonth(now.Year, now.Month), EarnedSalary = earned, DueAmount = due, Currency = CurrencyForEmployee(employee), ManualAmountApplied = manualApplied });
    }

    private async Task<decimal> CalculateUnpaidCommissionUsd(string employeeId, string? employeeCountry, DateTime now, CancellationToken ct)
    {
        var rate = await context.EmployeeBonusRates.AsNoTracking().FirstOrDefaultAsync(item => item.EmployeeId == employeeId, ct); if (rate is null) return 0m;
        var systemStart = new DateTime(2026, 4, 11, 10, 30, 0); var currentCycle = new DateTime(now.Year, now.Month, 1, 10, 30, 0); if (now < currentCycle) currentCycle = currentCycle.AddMonths(-1); if (currentCycle <= systemStart) return 0m;
        int[] success = [OrderStatusCodes.Delivered, OrderStatusCodes.BalanceUpdated, OrderStatusCodes.Paid];
        IQueryable<Order> Scope() => context.Orders.AsNoTracking().Where(item => success.Contains(item.OrderStatus) && item.CreatedDate >= systemStart && item.CreatedDate < currentCycle);
        var candidates = Scope().Where(item => item.ApplicationUserId == employeeId).Union(Scope().Where(item => item.Fixedby == employeeId)).Union(Scope().Where(item => item.Editedby == employeeId)).Union(Scope().Where(item => context.OrderEditHistories.Any(history => history.OrderId == item.Id && history.Editedby == employeeId)));
        var orders = await candidates.ToListAsync(ct); if (orders.Count == 0) return 0m; var ids = orders.Select(item => item.Id).ToArray();
        var successDates = await context.OrderStatusHistories.AsNoTracking().Where(item => item.OrderId.HasValue && ids.Contains(item.OrderId.Value) && item.Status.HasValue && success.Contains(item.Status.Value)).GroupBy(item => item.OrderId!.Value).Select(group => new { Id = group.Key, At = group.Max(item => item.CreatedAt) }).ToDictionaryAsync(item => item.Id, item => item.At, ct);
        var edits = await context.OrderEditHistories.AsNoTracking().Where(item => ids.Contains(item.OrderId)).OrderBy(item => item.OrderId).ThenBy(item => item.EditNumber).ToListAsync(ct); var editGroups = edits.GroupBy(item => item.OrderId).ToDictionary(group => group.Key, group => group.ToList());
        var fx = await context.ExchangeRates.AsNoTracking().ToDictionaryAsync(item => item.Country, item => item.SellToUSD, ct);
        decimal ToUsd(decimal amount, int country) => fx.GetValueOrDefault(country) > 0 ? amount / fx[country] : amount;
        var stakes = new List<BonusStake>();
        foreach (var order in orders) { var at = successDates.GetValueOrDefault(order.Id, order.CreatedDate); if (at < systemStart || at >= currentCycle) continue; var share = CalculateShare(order, editGroups.GetValueOrDefault(order.Id) ?? [], employeeId, ToUsd); if (share.Profit > 0 || order.ApplicationUserId == employeeId) stakes.Add(new(at, order.BonusPaymentId.HasValue, share.Creation, share.Processing)); }
        var localFx = CountryId(employeeCountry) is int id && fx.GetValueOrDefault(id) > 0 ? fx[id] : 1m; var proThreshold = rate.ProThreshold / localFx; var minimum = rate.MinimumBonusThreshold / localFx; decimal total = 0m;
        foreach (var cycle in stakes.GroupBy(item => CycleStart(item.At))) { var cycleProfit = cycle.Sum(item => item.Creation + item.Processing); var pro = proThreshold > 0 && cycleProfit >= proThreshold; if (minimum > 0 && cycleProfit < minimum) continue; var creatorPct = pro ? rate.ProBonusPercentage : rate.BonusPercentage; var processorPct = pro ? rate.ProBonusProcessingPercentage : rate.BonusProcessingPercentage; total += cycle.Where(item => !item.Paid).Sum(item => Math.Max(0, item.Creation * creatorPct / 100m) + Math.Max(0, item.Processing * processorPct / 100m)); }
        return total;
    }

    private static (decimal Profit, decimal Creation, decimal Processing) CalculateShare(Order order, IReadOnlyList<OrderEditHistory> edits, string employeeId, Func<decimal, int, decimal> usd)
    {
        var creator = order.ApplicationUserId ?? string.Empty; var states = edits.Select(item => (Profit: usd(item.TotalPrice - item.DeliveryPrice, item.Country), Author: string.IsNullOrWhiteSpace(item.Editedby) ? creator : item.Editedby!, At: item.LastEditedDate ?? item.CreatedDate)).ToList(); states.Add((usd(order.TotalPrice - order.DeliveryPrice, order.Country), string.IsNullOrWhiteSpace(order.Editedby) ? creator : order.Editedby!, order.LastEditedDate ?? order.CreatedDate)); if (states.Count == 0) return default;
        var tranches = new List<BonusTranche> { new(creator, Math.Max(0, states[0].Profit), order.CreatedDate) };
        for (var index = 1; index < states.Count; index++) { var delta = states[index].Profit - states[index - 1].Profit; if (delta > 0) tranches.Add(new(states[index].Author, delta, states[index].At)); else for (var cursor = tranches.Count - 1; cursor >= 0 && delta < 0; cursor--) { var take = Math.Min(tranches[cursor].Amount, -delta); tranches[cursor] = tranches[cursor] with { Amount = tranches[cursor].Amount - take }; delta += take; } }
        var processor = !string.IsNullOrWhiteSpace(order.Fixedby) && order.Fixedby != creator ? order.Fixedby : null; decimal creation = 0m, processing = 0m; foreach (var tranche in tranches.Where(item => item.Amount > 0)) { var amount = tranche.Amount; if (processor is not null && (!order.FixedOrderDate.HasValue || tranche.At <= order.FixedOrderDate.Value)) { amount /= 2m; if (processor == employeeId) processing += amount; } if (tranche.Owner == employeeId) creation += amount; } return (creation + processing, creation, processing);
    }
    private static DateTime CycleStart(DateTime at) { var start = new DateTime(at.Year, at.Month, 1, 10, 30, 0); return at < start ? start.AddMonths(-1) : start; }
    private sealed record BonusStake(DateTime At, bool Paid, decimal Creation, decimal Processing);
    private sealed record BonusTranche(string Owner, decimal Amount, DateTime At);

    private async Task<IActionResult> DeliveryAccounts(bool paid, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var rows = await (from item in context.Orders.AsNoTracking()
                          join company in context.DeliveryCompanies.AsNoTracking() on item.DeliveryCompanyId equals company.Id
                          where company.UserId == userId && item.IsPaid == paid
                          group item by new { company.Id, company.Name, item.Country } into grouped
                          select new { DeliveryCompanyId = grouped.Key.Id, DeliveryCompanyName = grouped.Key.Name, grouped.Key.Country, OrderCount = grouped.Count(), TotalPrice = grouped.Sum(order => order.TotalPrice), DeliveryPrice = grouped.Sum(order => order.DeliveryPrice) }).ToListAsync(ct);
        return Ok(rows);
    }

    private IQueryable<Employee> CurrentEmployee()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId == userId);
    }

    private static object EmptyReceivedSalary() => new { TotalEarned = "0.00", TotalEarnedUSD = "0.00", TotalReceived = 0m, Currency = string.Empty, Payments = Array.Empty<object>() };
    private static bool IsPositive(EmployeeTransactionType type) => type is EmployeeTransactionType.Bonus or EmployeeTransactionType.Advance or EmployeeTransactionType.Overtime;
    private static string TransactionTypeText(EmployeeTransactionType type) => type == EmployeeTransactionType.Overtime ? "ساعات دوام إضافية" : type.ToString();
    private static string CurrencyForEmployee(Employee employee) => CurrencyForCountryName(string.IsNullOrWhiteSpace(employee.Country) ? employee.Nationality : employee.Country);
    private static string CurrencyForCountry(int country) => country switch { 1 => "IQD", 2 => "AED", 3 => "QAR", 4 => "LYD", 5 => "OMR", 6 => "ILS", 7 => "TRY", 8 => "JOD", 9 => "KWD", 10 => "BHD", 11 => "SAR", 12 => "TND", 13 => "MAD", 14 => "DZD", 15 => "LBP", 16 => "EGP", _ => "USD" };
    private static string CurrencyForCountryName(string? country) { var value = (country ?? string.Empty).ToLowerInvariant(); if (value.Contains("مصر") || value.Contains("egypt")) return "EGP"; if (value.Contains("ترك") || value.Contains("turkey")) return "TRY"; if (value.Contains("عراق") || value.Contains("iraq")) return "IQD"; return "USD"; }
    private static int? CountryId(string? country) { var value = (country ?? string.Empty).ToLowerInvariant(); if (value.Contains("مصر") || value.Contains("egypt")) return 16; if (value.Contains("ترك") || value.Contains("turkey")) return 7; if (value.Contains("عراق") || value.Contains("iraq")) return 1; return null; }
}

public sealed record EmployeeSalaryReceivedRow(string MonthText, decimal Amount, string Currency, bool IsHistorical);
