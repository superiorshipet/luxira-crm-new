using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/operations/center")]
[Route("OperationsCenter")]
public class OperationsCenterController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OperationsCenterController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetLiveStats")]
    public async Task<IActionResult> GetLiveStats([FromQuery] int? country, CancellationToken ct)
    {
        var ordersQuery = _context.Orders.AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            ordersQuery = ordersQuery.Where(o => o.Country == country.Value);
        }

        int totalOrders = await ordersQuery.CountAsync(ct);
        int pendingOrders = await ordersQuery.CountAsync(o => o.OrderStatus == OrderStatusCodes.New, ct);
        int preparedOrders = await ordersQuery.CountAsync(o => o.OrderStatus == OrderStatusCodes.Prepared, ct);
        int inTransitOrders = await ordersQuery.CountAsync(o => o.OrderStatus == OrderStatusCodes.InDelivery, ct);
        int deliveredOrders = await ordersQuery.CountAsync(o => o.OrderStatus == OrderStatusCodes.Delivered, ct);
        int returnedOrders = await ordersQuery.CountAsync(o => o.OrderStatus == OrderStatusCodes.Returned, ct);

        return Ok(new
        {
            totalOrders,
            pendingOrders,
            preparedOrders,
            inTransitOrders,
            deliveredOrders,
            returnedOrders,
            serverTime = DateTime.UtcNow
        });
    }

    [HttpGet("/OperationsCenter/Index")]
    public async Task<IActionResult> Index(
        [FromQuery] DateTime? day,
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        [FromQuery] bool? useAutoPeriod,
        CancellationToken ct)
    {
        var period = ResolvePeriod(day, fromDate, toDate, useAutoPeriod);
        var snapshot = await BuildSnapshotAsync(period.Start, period.End, ct);
        return Ok(new
        {
            filterDay = period.Start.Date,
            filterFromDate = period.Start.ToString("yyyy-MM-dd"),
            filterToDate = period.End.AddDays(-1).ToString("yyyy-MM-dd"),
            isAutoPeriodFilter = period.IsAutomatic,
            periodStart = period.Start,
            periodEnd = period.End,
            generatedAt = DateTime.UtcNow,
            snapshot.TotalOrders,
            snapshot.ProcessedOrders,
            snapshot.DeliveredOrders,
            snapshot.FailedDeliveryOrders,
            snapshot.IncompleteOrders,
            snapshot.MorningCustomers,
            snapshot.EveningCustomers,
            snapshot.TotalOrdersPrice,
            snapshot.NoAdOrders,
            snapshot.Stores,
            snapshot.Countries,
            snapshot.Employees
        });
    }

    [HttpGet("/OperationsCenter/GetSupplementalMetrics")]
    public async Task<IActionResult> GetSupplementalMetrics(
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        CancellationToken ct)
    {
        var period = ResolvePeriod(null, fromDate, toDate, null);
        var snapshot = await BuildSnapshotAsync(period.Start, period.End, ct);

        var problemReports = await _context.OrderPosts.AsNoTracking()
            .CountAsync(post => post.Type == OrderPostType.Problem && post.CreatedAt >= period.Start && post.CreatedAt < period.End, ct);
        var immediateEditReports = await _context.OrderPosts.AsNoTracking()
            .CountAsync(post => post.Type == OrderPostType.EditNote && post.CreatedAt >= period.Start && post.CreatedAt < period.End, ct);
        var employeeErrors = await _context.EmployeeErrors.AsNoTracking()
            .CountAsync(error => !error.IsDeleted && error.CreatedAt >= period.Start && error.CreatedAt < period.End, ct);
        var transactionTotals = await _context.EmployeeTransactions.AsNoTracking()
            .Where(transaction => !transaction.IsDeleted && transaction.Date >= period.Start && transaction.Date < period.End)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Deductions = group.Where(transaction => transaction.TransactionType == EmployeeTransactionType.Deduction).Sum(transaction => transaction.Amount),
                Advances = group.Where(transaction => transaction.TransactionType == EmployeeTransactionType.Advance).Sum(transaction => transaction.Amount)
            })
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                totalOrdersPriceInDollar = snapshot.TotalOrdersPrice,
                totalOrdersPriceInTL = snapshot.TotalOrdersPrice,
                totalWebsiteDomains = await _context.WebsiteDomains.AsNoTracking().CountAsync(domain => !domain.IsDeleted, ct),
                problemReports,
                immediateEditReports,
                employeeErrors,
                noAdOrders = snapshot.NoAdOrders,
                lateEmployees = 0,
                totalDeductions = transactionTotals?.Deductions ?? 0m,
                totalAdvances = transactionTotals?.Advances ?? 0m,
                totalFinancialTransfers = await _context.Orders.AsNoTracking()
                    .Where(order => order.IsPaid && order.CreatedDate >= period.Start && order.CreatedDate < period.End)
                    .SumAsync(order => (decimal?)order.TotalPrice, ct) ?? 0m
            }
        });
    }

    [HttpGet("/OperationsCenter/GetCustomerDeliveryOrders")]
    public async Task<IActionResult> GetCustomerDeliveryOrders(
        [FromQuery] string? fromDate,
        [FromQuery] string? toDate,
        [FromQuery] string? employeeId,
        [FromQuery] string? storeKey,
        [FromQuery] string? countryKey,
        CancellationToken ct)
    {
        var period = ResolvePeriod(null, fromDate, toDate, null);
        var query = _context.Orders.AsNoTracking().Where(order =>
            order.CustomerDeliveryPrice > 0 &&
            (order.InstantAddedDate ?? order.CreatedDate) >= period.Start &&
            (order.InstantAddedDate ?? order.CreatedDate) < period.End);

        if (!string.IsNullOrWhiteSpace(employeeId)) query = query.Where(order => order.ApplicationUserId == employeeId);
        if (int.TryParse(storeKey, out var storeId)) query = query.Where(order => order.ManufacturingCompanyId == storeId);
        if (int.TryParse(countryKey, out var countryId)) query = query.Where(order => order.Country == countryId);

        var items = await query
            .OrderByDescending(order => order.InstantAddedDate ?? order.CreatedDate)
            .ThenByDescending(order => order.Id)
            .Take(500)
            .Select(order => new
            {
                orderId = order.Id,
                shipmentCode = (order.ExternalOrderId ?? order.Id).ToString(),
                order.CustomerName,
                order.TelephoneNumber,
                order.State,
                order.Address,
                orderStatusText = OrderStatusCodes.GetDisplayName(order.OrderStatus),
                order.TotalPrice,
                order.DeliveryPrice,
                createdAt = order.InstantAddedDate ?? order.CreatedDate,
                countryKey = order.Country.ToString(),
                countryName = order.Country.ToString(),
                storeKey = order.ManufacturingCompanyId.HasValue ? order.ManufacturingCompanyId.Value.ToString() : "",
                storeName = _context.ManufacturingCompanies.Where(store => store.Id == order.ManufacturingCompanyId).Select(store => store.Name).FirstOrDefault(),
                employeeId = order.ApplicationUserId,
                employeeName = _context.Employees.Where(employee => employee.ApplicationUserId == order.ApplicationUserId).Select(employee => employee.DisplayName ?? employee.Name).FirstOrDefault(),
                order.CustomerDeliveryPrice
            })
            .ToListAsync(ct);

        return Ok(new
        {
            success = true,
            totalCount = items.Count,
            totalCustomerDeliveryPrice = items.Sum(item => item.CustomerDeliveryPrice),
            periodStart = period.Start,
            periodEnd = period.End,
            items
        });
    }

    private async Task<OperationsSnapshot> BuildSnapshotAsync(DateTime start, DateTime end, CancellationToken ct)
    {
        var rows = await _context.Orders.AsNoTracking()
            .Where(order => (order.InstantAddedDate ?? order.CreatedDate) >= start && (order.InstantAddedDate ?? order.CreatedDate) < end)
            .Select(order => new OperationsOrderRow(
                order.Id,
                order.OrderStatus,
                order.FixedOrderDate,
                order.InstantAddedDate ?? order.CreatedDate,
                order.TotalPrice,
                order.PhotoUrl,
                order.CampaignId,
                order.Country,
                order.ManufacturingCompanyId,
                order.ApplicationUserId))
            .ToListAsync(ct);

        var stores = rows
            .Where(row => row.StoreId.HasValue)
            .GroupBy(row => row.StoreId!.Value)
            .Select(group => new OperationsDimensionTotal(group.Key.ToString(), group.Count(), group.Sum(row => row.TotalPrice)))
            .OrderByDescending(item => item.TotalOrders)
            .ToArray();
        var countries = rows
            .GroupBy(row => row.Country)
            .Select(group => new OperationsDimensionTotal(group.Key.ToString(), group.Count(), group.Sum(row => row.TotalPrice)))
            .OrderByDescending(item => item.TotalOrders)
            .ToArray();
        var employees = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.EmployeeId))
            .GroupBy(row => row.EmployeeId!)
            .Select(group => new OperationsDimensionTotal(group.Key, group.Count(), group.Sum(row => row.TotalPrice)))
            .OrderByDescending(item => item.TotalOrders)
            .ToArray();

        return new OperationsSnapshot(
            rows.Count,
            rows.Count(row => row.FixedOrderDate >= start && row.FixedOrderDate < end),
            rows.Count(row => row.Status == OrderStatusCodes.Delivered),
            rows.Count(row => OrderStatusCodes.FailureStatuses.Contains(row.Status)),
            rows.Count(row => row.Status is OrderStatusCodes.Incomplete
                or OrderStatusCodes.IncompleteStage1
                or OrderStatusCodes.IncompleteStage2
                or OrderStatusCodes.IncompleteStage3
                or OrderStatusCodes.IncompleteStage4
                or OrderStatusCodes.IncompleteStage5
                or OrderStatusCodes.IncompleteStage6),
            rows.Count(row => row.CreatedAt.Hour < 12),
            rows.Count(row => row.CreatedAt.Hour >= 12),
            rows.Sum(row => row.TotalPrice),
            rows.Count(row => !row.CampaignId.HasValue),
            stores,
            countries,
            employees);
    }

    private static (DateTime Start, DateTime End, bool IsAutomatic) ResolvePeriod(
        DateTime? day,
        string? fromDate,
        string? toDate,
        bool? useAutoPeriod)
    {
        var from = ParseDate(fromDate) ?? day?.Date;
        var to = ParseDate(toDate) ?? day?.Date;
        if (!from.HasValue && to.HasValue) from = to;
        if (from.HasValue && !to.HasValue) to = from;
        var automatic = useAutoPeriod == true || !from.HasValue || !to.HasValue;
        if (automatic)
        {
            var now = DateTime.Now;
            var operationalDay = now.TimeOfDay < TimeSpan.FromHours(10.5) ? now.Date.AddDays(-1) : now.Date;
            var start = operationalDay.AddHours(10.5);
            return (start, start.AddDays(1), true);
        }

        var explicitStart = from!.Value.Date.AddHours(10.5);
        var explicitEnd = to!.Value.Date.AddHours(10.5);
        if (explicitEnd <= explicitStart) explicitEnd = explicitStart.AddDays(1);
        return (explicitStart, explicitEnd, false);
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed.Date
            : null;
}

public sealed record OperationsOrderRow(
    int Id,
    int Status,
    DateTime? FixedOrderDate,
    DateTime CreatedAt,
    decimal TotalPrice,
    string? PhotoUrl,
    int? CampaignId,
    int Country,
    int? StoreId,
    string? EmployeeId);

public sealed record OperationsDimensionTotal(string Key, int TotalOrders, decimal TotalAmount);

public sealed record OperationsSnapshot(
    int TotalOrders,
    int ProcessedOrders,
    int DeliveredOrders,
    int FailedDeliveryOrders,
    int IncompleteOrders,
    int MorningCustomers,
    int EveningCustomers,
    decimal TotalOrdersPrice,
    int NoAdOrders,
    IReadOnlyList<OperationsDimensionTotal> Stores,
    IReadOnlyList<OperationsDimensionTotal> Countries,
    IReadOnlyList<OperationsDimensionTotal> Employees);
