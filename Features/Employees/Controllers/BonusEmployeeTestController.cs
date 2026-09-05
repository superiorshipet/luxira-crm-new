using System.Diagnostics;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("BonusEmployeeTest")]
public sealed class BonusEmployeeTestController(ApplicationDbContext context) : ControllerBase
{
    private static readonly DateTime SystemStart = new(2026, 4, 11, 10, 30, 0);

    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? employeeId, CancellationToken ct)
    {
        var employees = await context.Employees.AsNoTracking()
            .Where(employee => employee.IsActive && employee.ApplicationUserId != null)
            .OrderBy(employee => employee.DisplayName ?? employee.Name)
            .Select(employee => new { employee.ApplicationUserId, Name = employee.DisplayName ?? employee.Name })
            .ToListAsync(ct);
        return Ok(new { employeeId, employees, now = DateTimeOffset.UtcNow });
    }

    [HttpGet("RunPartial")]
    public async Task<IActionResult> RunPartial(
        [FromQuery] string? employeeId,
        [FromQuery] bool onlyUnpaid = true,
        [FromQuery] int rowCap = 400,
        CancellationToken ct = default)
    {
        rowCap = Math.Clamp(rowCap, 1, 2000);
        var employeeIds = await context.EmployeeBonusRates.AsNoTracking()
            .Where(rate => string.IsNullOrWhiteSpace(employeeId) || rate.EmployeeId == employeeId)
            .OrderBy(rate => rate.EmployeeId)
            .Select(rate => rate.EmployeeId)
            .Take(rowCap)
            .ToListAsync(ct);

        var stopwatch = Stopwatch.StartNew();
        var rows = new List<EmployeeBonusDiagnostic>(employeeIds.Count);
        foreach (var id in employeeIds)
            rows.Add(await CalculateEmployeeAsync(id, onlyUnpaid, ct));
        stopwatch.Stop();

        return Ok(new
        {
            onlyUnpaid,
            systemStart = SystemStart,
            currentCycleStart = CurrentCycleStart(),
            elapsedMs = stopwatch.ElapsedMilliseconds,
            rows,
        });
    }

    [HttpGet("CutoffPartial")]
    public async Task<IActionResult> CutoffPartial([FromQuery] string? employeeId, CancellationToken ct)
    {
        var currentCycle = CurrentCycleStart();
        var query = context.EmployeeBonusPayments.AsNoTracking().Where(payment => payment.DatePaid < currentCycle);
        if (!string.IsNullOrWhiteSpace(employeeId)) query = query.Where(payment => payment.EmployeeId == employeeId);
        var closed = await query.GroupBy(payment => payment.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, Amount = group.Sum(payment => payment.AmountPaid), Payments = group.Count() })
            .ToListAsync(ct);
        return Ok(new { systemStart = SystemStart, currentCycleStart = currentCycle, closedCyclePayments = closed });
    }

    [HttpGet("OrderPartial")]
    public async Task<IActionResult> OrderPartial([FromQuery] int orderId, [FromQuery] bool onlyUnpaid = true, CancellationToken ct = default)
    {
        var order = await context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return NotFound();

        var contributorIds = new[] { order.ApplicationUserId, order.Editedby, order.Fixedby }
            .Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToHashSet(StringComparer.Ordinal);
        contributorIds.UnionWith(await context.OrderEditHistories.AsNoTracking()
            .Where(history => history.OrderId == orderId && history.Editedby != null && history.Editedby != "")
            .Select(history => history.Editedby!).Distinct().ToListAsync(ct));

        var rows = new List<object>();
        foreach (var id in contributorIds)
        {
            var diagnostic = await CalculateEmployeeAsync(id, onlyUnpaid, ct);
            rows.Add(new
            {
                employeeId = id,
                diagnostic.Rate,
                diagnostic.Cycles,
                orderRows = diagnostic.Orders.Where(item => item.OrderId == orderId),
            });
        }

        return Ok(new
        {
            order = new { order.Id, order.OrderStatus, order.CreatedDate, order.ApplicationUserId, order.Editedby, order.Fixedby, order.BonusPaymentId },
            eligible = IsSuccess(order.OrderStatus) && (!onlyUnpaid || !order.BonusPaymentId.HasValue),
            contributors = rows,
        });
    }

    private async Task<EmployeeBonusDiagnostic> CalculateEmployeeAsync(string employeeId, bool onlyUnpaid, CancellationToken ct)
    {
        var rate = await context.EmployeeBonusRates.AsNoTracking().FirstOrDefaultAsync(item => item.EmployeeId == employeeId, ct);
        var employeeCountry = await context.Employees.AsNoTracking()
            .Where(item => item.ApplicationUserId == employeeId)
            .Select(item => item.Country ?? item.Nationality)
            .FirstOrDefaultAsync(ct);
        if (rate is null) return new(employeeId, null, [], []);

        var currentCycle = CurrentCycleStart();
        int[] successStatuses = [OrderStatusCodes.Delivered, OrderStatusCodes.BalanceUpdated, OrderStatusCodes.Paid];
        IQueryable<Order> Scope() => context.Orders.AsNoTracking()
            .Where(item => successStatuses.Contains(item.OrderStatus) && item.CreatedDate >= SystemStart && item.CreatedDate < currentCycle);
        var candidates = Scope().Where(item => item.ApplicationUserId == employeeId)
            .Union(Scope().Where(item => item.Fixedby == employeeId))
            .Union(Scope().Where(item => item.Editedby == employeeId))
            .Union(Scope().Where(item => context.OrderEditHistories.Any(history => history.OrderId == item.Id && history.Editedby == employeeId)));
        var orders = await candidates.ToListAsync(ct);
        if (orders.Count == 0) return new(employeeId, rate, [], []);

        var orderIds = orders.Select(item => item.Id).ToArray();
        var successDates = await context.OrderStatusHistories.AsNoTracking()
            .Where(item => item.OrderId.HasValue && orderIds.Contains(item.OrderId.Value) &&
                           item.Status.HasValue && successStatuses.Contains(item.Status.Value))
            .GroupBy(item => item.OrderId!.Value)
            .Select(group => new { Id = group.Key, At = group.Max(item => item.CreatedAt) })
            .ToDictionaryAsync(item => item.Id, item => item.At, ct);
        var edits = await context.OrderEditHistories.AsNoTracking().Where(item => orderIds.Contains(item.OrderId))
            .OrderBy(item => item.OrderId).ThenBy(item => item.EditNumber).ToListAsync(ct);
        var editsByOrder = edits.GroupBy(item => item.OrderId).ToDictionary(group => group.Key, group => (IReadOnlyList<OrderEditHistory>)group.ToList());
        var exchangeRates = await context.ExchangeRates.AsNoTracking().ToDictionaryAsync(item => item.Country, item => item.SellToUSD, ct);
        decimal ToUsd(decimal amount, int country) => exchangeRates.GetValueOrDefault(country) > 0 ? amount / exchangeRates[country] : amount;

        var stakes = orders.Select(order =>
        {
            var accountingTime = successDates.GetValueOrDefault(order.Id, order.CreatedDate);
            var share = CalculateShare(order, editsByOrder.GetValueOrDefault(order.Id) ?? [], employeeId, ToUsd);
            return new BonusStake(order.Id, accountingTime, order.BonusPaymentId.HasValue, share.Creation, share.Processing);
        }).Where(item => item.At >= SystemStart && item.At < currentCycle && (item.Creation > 0 || item.Processing > 0)).ToList();

        var localRate = CountryId(employeeCountry) is int countryId && exchangeRates.GetValueOrDefault(countryId) > 0
            ? exchangeRates[countryId] : 1m;
        var proThresholdUsd = rate.ProThreshold / localRate;
        var minimumUsd = rate.MinimumBonusThreshold / localRate;
        var cycleRows = new List<BonusCycleDiagnostic>();
        var orderRows = new List<BonusOrderDiagnostic>();
        foreach (var cycle in stakes.GroupBy(item => CycleStart(item.At)).OrderBy(item => item.Key))
        {
            var totalProfit = cycle.Sum(item => item.Creation + item.Processing);
            var usePro = proThresholdUsd > 0 && totalProfit >= proThresholdUsd;
            var meetsMinimum = minimumUsd <= 0 || totalProfit >= minimumUsd;
            var creatorPct = usePro ? rate.ProBonusPercentage : rate.BonusPercentage;
            var processorPct = usePro ? rate.ProBonusProcessingPercentage : rate.BonusProcessingPercentage;
            var payable = cycle.Where(item => !onlyUnpaid || !item.Paid).Sum(item =>
                Math.Max(0, item.Creation * creatorPct / 100m) + Math.Max(0, item.Processing * processorPct / 100m));
            if (!meetsMinimum) payable = 0;
            cycleRows.Add(new(cycle.Key, cycle.Key.AddMonths(1), totalProfit, usePro, meetsMinimum, creatorPct, processorPct, payable));
            orderRows.AddRange(cycle.Where(item => !onlyUnpaid || !item.Paid).Select(item => new BonusOrderDiagnostic(
                item.OrderId, item.At, item.Paid, item.Creation, item.Processing,
                meetsMinimum ? Math.Max(0, item.Creation * creatorPct / 100m) + Math.Max(0, item.Processing * processorPct / 100m) : 0m)));
        }
        return new(employeeId, rate, cycleRows, orderRows);
    }

    private static (decimal Creation, decimal Processing) CalculateShare(Order order, IReadOnlyList<OrderEditHistory> edits, string employeeId, Func<decimal, int, decimal> usd)
    {
        var creator = order.ApplicationUserId ?? string.Empty;
        var states = edits.Select(item => (Profit: usd(item.TotalPrice - item.DeliveryPrice, item.Country), Author: string.IsNullOrWhiteSpace(item.Editedby) ? creator : item.Editedby!, At: item.LastEditedDate ?? item.CreatedDate)).ToList();
        states.Add((usd(order.TotalPrice - order.DeliveryPrice, order.Country), string.IsNullOrWhiteSpace(order.Editedby) ? creator : order.Editedby!, order.LastEditedDate ?? order.CreatedDate));
        var tranches = new List<BonusTranche> { new(creator, Math.Max(0, states[0].Profit), order.CreatedDate) };
        for (var index = 1; index < states.Count; index++)
        {
            var delta = states[index].Profit - states[index - 1].Profit;
            if (delta > 0) tranches.Add(new(states[index].Author, delta, states[index].At));
            else for (var cursor = tranches.Count - 1; cursor >= 0 && delta < 0; cursor--)
            {
                var take = Math.Min(tranches[cursor].Amount, -delta);
                tranches[cursor] = tranches[cursor] with { Amount = tranches[cursor].Amount - take };
                delta += take;
            }
        }
        var processor = !string.IsNullOrWhiteSpace(order.Fixedby) && order.Fixedby != creator ? order.Fixedby : null;
        decimal creation = 0, processing = 0;
        foreach (var tranche in tranches.Where(item => item.Amount > 0))
        {
            var amount = tranche.Amount;
            if (processor is not null && (!order.FixedOrderDate.HasValue || tranche.At <= order.FixedOrderDate.Value))
            {
                amount /= 2m;
                if (processor == employeeId) processing += amount;
            }
            if (tranche.Owner == employeeId) creation += amount;
        }
        return (creation, processing);
    }

    private static bool IsSuccess(int status) => status is OrderStatusCodes.Delivered or OrderStatusCodes.BalanceUpdated or OrderStatusCodes.Paid;
    private static DateTime CurrentCycleStart() => CycleStart(DateTime.UtcNow.AddHours(3));
    private static DateTime CycleStart(DateTime at) { var start = new DateTime(at.Year, at.Month, 1, 10, 30, 0); return at < start ? start.AddMonths(-1) : start; }
    private static int? CountryId(string? country) { var value = (country ?? string.Empty).ToLowerInvariant(); if (value.Contains("مصر") || value.Contains("egypt")) return 16; if (value.Contains("ترك") || value.Contains("turkey")) return 7; if (value.Contains("عراق") || value.Contains("iraq")) return 1; return null; }

    public sealed record EmployeeBonusDiagnostic(string EmployeeId, EmployeeBonusRate? Rate, IReadOnlyList<BonusCycleDiagnostic> Cycles, IReadOnlyList<BonusOrderDiagnostic> Orders);
    public sealed record BonusCycleDiagnostic(DateTime Start, DateTime End, decimal ProfitUsd, bool UsedProRate, bool MetMinimum, decimal CreatorRatePct, decimal ProcessorRatePct, decimal PayableBonusUsd);
    public sealed record BonusOrderDiagnostic(int OrderId, DateTime AccountingTime, bool Paid, decimal CreationProfitUsd, decimal ProcessingProfitUsd, decimal BonusUsd);
    private sealed record BonusStake(int OrderId, DateTime At, bool Paid, decimal Creation, decimal Processing);
    private sealed record BonusTranche(string Owner, decimal Amount, DateTime At);
}
