using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/ratings")]
[Route("Rating")]
public class RatingController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RatingController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetRatings")]
    public async Task<ActionResult<List<EmployeeRatingDto>>> GetRatings([FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = _context.EmployeeRatings
            .Include(r => r.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(r => r.EmployeeId == employeeId.Value);
        }

        var list = await query.OrderByDescending(r => r.RatedAt)
            .Select(r => new EmployeeRatingDto(r.Id, r.EmployeeId, r.Employee != null ? r.Employee.Name : null, r.Score, r.Feedback, r.RatedByUserId, r.RatedAt))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [HttpPost("SubmitRating")]
    public async Task<ActionResult<EmployeeRatingDto>> SubmitRating([FromBody] SubmitRatingRequest request, CancellationToken ct)
    {
        var r = new EmployeeRating
        {
            EmployeeId = request.EmployeeId,
            Score = request.Score,
            Feedback = request.Feedback,
            RatedByUserId = User.GetUserId() ?? "system",
            RatedAt = DateTime.UtcNow
        };

        await _context.EmployeeRatings.AddAsync(r, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new EmployeeRatingDto(r.Id, r.EmployeeId, null, r.Score, r.Feedback, r.RatedByUserId, r.RatedAt));
    }

    [HttpGet("/Rating/Employelist")]
    [HttpPost("/Rating/Employelist")]
    public async Task<IActionResult> Employelist([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var query = BuildOrderQuery(filter);
        var items = await (from order in query
                           join employee in _context.Employees.AsNoTracking() on order.ApplicationUserId equals employee.ApplicationUserId
                           where employee.IsActive && !employee.IsDeleted
                           group order by new { employee.Id, employee.ApplicationUserId, Name = employee.DisplayName ?? employee.Name } into groupRows
                           select new
                           {
                               employeeId = groupRows.Key.Id,
                               userId = groupRows.Key.ApplicationUserId,
                               employeeName = groupRows.Key.Name,
                               totalOrders = groupRows.Count(),
                               fixedOrders = groupRows.Count(order => order.FixedOrderDate != null),
                               totalAmount = groupRows.Sum(order => order.TotalPrice)
                           }).OrderByDescending(item => item.totalOrders).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/NoAdRatingSummary")]
    public async Task<IActionResult> NoAdRatingSummary([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var items = await EmployeeSummary(BuildOrderQuery(filter).Where(order => order.CampaignId == null)).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/NoAdRatingDetails")]
    public Task<IActionResult> NoAdRatingDetails([FromQuery] RatingOrderFilter filter, CancellationToken ct) =>
        RatingDetails(BuildOrderQuery(filter).Where(order => order.CampaignId == null), filter, ct);

    [HttpGet("/Rating/IncompleteCreatedOrdersRatingSummary")]
    public async Task<IActionResult> IncompleteCreatedOrdersRatingSummary([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var statuses = IncompleteStatuses;
        var items = await EmployeeSummary(BuildOrderQuery(filter).Where(order => statuses.Contains(order.OrderStatus))).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/IncompleteCreatedOrdersRatingDetails")]
    public Task<IActionResult> IncompleteCreatedOrdersRatingDetails([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var statuses = IncompleteStatuses;
        return RatingDetails(BuildOrderQuery(filter).Where(order => statuses.Contains(order.OrderStatus)), filter, ct);
    }

    [HttpGet("/Rating/CurrentShiftOrdersCount")]
    public async Task<IActionResult> CurrentShiftOrdersCount(CancellationToken ct)
    {
        var range = RatingRange(null, null);
        var count = await _context.Orders.AsNoTracking().CountAsync(order => order.ApplicationUserId == User.GetUserId() && order.InstantAddedDate >= range.Start && order.InstantAddedDate < range.End, ct);
        return Ok(new { count });
    }

    [HttpGet("/Rating/SalesIndicatorPriceLevelSummary")]
    public async Task<IActionResult> SalesIndicatorPriceLevelSummary([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var items = await SalesIndicatorRows(filter)
            .GroupBy(row => new { row.EmployeeId, row.EmployeeName })
            .Select(group => new
            {
                employeeId = group.Key.EmployeeId,
                employeeName = group.Key.EmployeeName,
                minimumCount = group.Count(row => row.PriceGroup == "minimum"),
                middleCount = group.Count(row => row.PriceGroup == "middle"),
                basicCount = group.Count(row => row.PriceGroup == "basic")
            }).OrderByDescending(item => item.minimumCount + item.middleCount + item.basicCount).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/SalesIndicatorPriceLevelDetails")]
    public async Task<IActionResult> SalesIndicatorPriceLevelDetails([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var priceGroup = NormalizePriceGroup(filter.PriceGroup);
        if (priceGroup is null) return BadRequest(new { success = false, message = "نوع مؤشر البيع غير صحيح." });
        var query = SalesIndicatorRows(filter).Where(row => row.PriceGroup == priceGroup);
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize ?? 50, 1, 200);
        var totalItems = await query.Select(row => row.OrderId).Distinct().CountAsync(ct);
        var items = await query.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.OrderId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { success = true, priceGroup, items, currentPage = page, pageSize, totalItems });
    }

    [HttpGet("/Rating/StoreList")]
    [HttpPost("/Rating/StoreList")]
    public async Task<IActionResult> StoreList([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var items = await BuildOrderQuery(filter).Where(order => order.ManufacturingCompanyId != null)
            .GroupBy(order => order.ManufacturingCompanyId!.Value)
            .Select(group => new
            {
                storeId = group.Key,
                storeName = _context.ManufacturingCompanies.Where(store => store.Id == group.Key).Select(store => store.Name).FirstOrDefault(),
                totalOrders = group.Count(),
                totalAmount = group.Sum(order => order.TotalPrice),
                deliveredCount = group.Count(order => DeliveredStatuses.Contains(order.OrderStatus)),
                failedCount = group.Count(order => OrderStatusCodes.FailureStatuses.Contains(order.OrderStatus))
            }).OrderByDescending(item => item.totalOrders).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/CountryList")]
    [HttpPost("/Rating/CountryList")]
    public async Task<IActionResult> CountryList([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var items = await BuildOrderQuery(filter).GroupBy(order => order.Country).Select(group => new
        {
            countryId = group.Key,
            totalOrders = group.Count(),
            totalAmount = group.Sum(order => order.TotalPrice),
            deliveredCount = group.Count(order => DeliveredStatuses.Contains(order.OrderStatus)),
            failedCount = group.Count(order => OrderStatusCodes.FailureStatuses.Contains(order.OrderStatus))
        }).OrderByDescending(item => item.totalOrders).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/ManufactureCompanyDetails")]
    [HttpPost("/Rating/ManufactureCompanyDetails")]
    public async Task<IActionResult> ManufactureCompanyDetails([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var items = await BuildOrderQuery(filter).Where(order => order.ManufacturingCompanyId != null)
            .GroupBy(order => new { order.ManufacturingCompanyId, order.Country }).Select(group => new
            {
                storeId = group.Key.ManufacturingCompanyId,
                countryId = group.Key.Country,
                storeName = _context.ManufacturingCompanies.Where(store => store.Id == group.Key.ManufacturingCompanyId).Select(store => store.Name).FirstOrDefault(),
                totalOrders = group.Count(),
                totalAmount = group.Sum(order => order.TotalPrice)
            }).OrderByDescending(item => item.totalOrders).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/CityByCountryDetails")]
    [HttpPost("/Rating/CityByCountryDetails")]
    public async Task<IActionResult> CityByCountryDetails([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var items = await BuildOrderQuery(filter).GroupBy(order => new { order.Country, order.State }).Select(group => new
        {
            countryId = group.Key.Country,
            city = group.Key.State,
            totalOrders = group.Count(),
            totalAmount = group.Sum(order => order.TotalPrice)
        }).OrderByDescending(item => item.totalOrders).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/FailedAndDeliveredOrders")]
    [HttpPost("/Rating/FailedAndDeliveredOrders")]
    public async Task<IActionResult> FailedAndDeliveredOrders([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var relevant = DeliveredStatuses.Concat(OrderStatusCodes.FailureStatuses).ToArray();
        var items = await BuildOrderQuery(filter).Where(order => relevant.Contains(order.OrderStatus))
            .GroupBy(order => order.ApplicationUserId).Select(group => new
            {
                employeeUserId = group.Key,
                employeeName = _context.Employees.Where(employee => employee.ApplicationUserId == group.Key).Select(employee => employee.DisplayName ?? employee.Name).FirstOrDefault(),
                deliveredCount = group.Count(order => DeliveredStatuses.Contains(order.OrderStatus)),
                failedCount = group.Count(order => OrderStatusCodes.FailureStatuses.Contains(order.OrderStatus)),
                totalOrders = group.Count()
            }).OrderByDescending(item => item.totalOrders).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/EmployeeOrderStatusGroupSummary")]
    public async Task<IActionResult> EmployeeOrderStatusGroupSummary([FromQuery] RatingOrderFilter filter, CancellationToken ct)
    {
        var items = await (from order in BuildOrderQuery(filter)
                           join employee in _context.Employees.AsNoTracking() on order.ApplicationUserId equals employee.ApplicationUserId
                           where employee.IsActive && !employee.IsDeleted
                           group order by new { employee.Id, employee.ApplicationUserId, Name = employee.DisplayName ?? employee.Name } into groupRows
                           select new
                           {
                               employeeId = groupRows.Key.Id,
                               userId = groupRows.Key.ApplicationUserId,
                               employeeName = groupRows.Key.Name,
                               deliveredCount = groupRows.Count(order => DeliveredStatuses.Contains(order.OrderStatus)),
                               failedCount = groupRows.Count(order => OrderStatusCodes.FailureStatuses.Contains(order.OrderStatus)),
                               delayedCount = groupRows.Count(order => order.OrderStatus == OrderStatusCodes.Postponed)
                           }).ToListAsync(ct);
        return Ok(new { success = true, items });
    }

    [HttpGet("/Rating/EmployeOrdersDetails")]
    [HttpPost("/Rating/EmployeOrdersDetails")]
    public Task<IActionResult> EmployeOrdersDetails([FromQuery] RatingOrderFilter filter, CancellationToken ct) => RatingDetails(BuildOrderQuery(filter), filter, ct);

    private IQueryable<Order> BuildOrderQuery(RatingOrderFilter filter)
    {
        var range = RatingRange(filter.StartDate ?? filter.StartDay, filter.EndDate ?? filter.EndDay);
        var query = _context.Orders.AsNoTracking().Where(order => (order.InstantAddedDate ?? order.CreatedDate) >= range.Start && (order.InstantAddedDate ?? order.CreatedDate) < range.End);
        var employeeId = filter.EmployeeId ?? filter.EmployeeUserId;
        if (!string.IsNullOrWhiteSpace(employeeId)) query = query.Where(order => order.ApplicationUserId == employeeId);
        var country = filter.CountryId ?? filter.CountyId;
        if (country.HasValue) query = query.Where(order => order.Country == country);
        if (filter.OrderSourceId.HasValue) query = query.Where(order => order.OrderSource == filter.OrderSourceId);
        if (filter.GenderId.HasValue) query = query.Where(order => order.Gender == filter.GenderId);
        if (filter.StoreId is > 0) query = query.Where(order => order.ManufacturingCompanyId == filter.StoreId);
        if (filter.DeliveryCompanyId is > 0) query = query.Where(order => order.DeliveryCompanyId == filter.DeliveryCompanyId);
        if (filter.MainWarehouseId is > 0) query = query.Where(order => order.OrderWarehouses.Any(item => _context.Warehouses.Any(warehouse => warehouse.Id == item.WarehouseId && warehouse.MainWarehouseId == filter.MainWarehouseId)));
        if (filter.WarehouseId is > 0) query = query.Where(order => order.OrderWarehouses.Any(item => item.WarehouseId == filter.WarehouseId));
        if (filter.FromComments.HasValue) query = query.Where(order => order.FromComments == filter.FromComments);
        if (filter.IsFixed.HasValue) query = filter.IsFixed.Value ? query.Where(order => order.FixedOrderDate != null) : query.Where(order => order.FixedOrderDate == null);
        return query;
    }

    private IQueryable<SalesIndicatorRatingRow> SalesIndicatorRows(RatingOrderFilter filter) =>
        from order in BuildOrderQuery(filter)
        join orderItem in _context.OrderWarehouses.AsNoTracking() on order.Id equals orderItem.OrderId
        join warehouse in _context.Warehouses.AsNoTracking() on orderItem.WarehouseId equals warehouse.Id
        join rule in _context.SalesIndicators.AsNoTracking()
            on new { order.Country, MainWarehouseId = warehouse.MainWarehouseId!.Value, Quantity = orderItem.Amount }
            equals new { rule.Country, rule.MainWarehouseId, Quantity = rule.Quantity }
        where warehouse.MainWarehouseId.HasValue && order.ApplicationUserId != null &&
              ((order.TotalPrice >= rule.MinimumSellingFrom && order.TotalPrice <= rule.MinimumSellingTo) ||
               (order.TotalPrice >= rule.MiddleSellingFrom && order.TotalPrice <= rule.MiddleSellingTo) ||
               (order.TotalPrice >= rule.BasicSellingFrom && order.TotalPrice <= rule.BasicSellingTo))
        select new SalesIndicatorRatingRow(
            order.Id,
            order.ApplicationUserId!,
            _context.Employees.Where(employee => employee.ApplicationUserId == order.ApplicationUserId).Select(employee => employee.DisplayName ?? employee.Name).FirstOrDefault() ?? "",
            order.CustomerName,
            order.TelephoneNumber,
            order.TotalPrice,
            order.Country,
            order.InstantAddedDate ?? order.CreatedDate,
            order.TotalPrice >= rule.MinimumSellingFrom && order.TotalPrice <= rule.MinimumSellingTo ? "minimum" :
            order.TotalPrice >= rule.MiddleSellingFrom && order.TotalPrice <= rule.MiddleSellingTo ? "middle" : "basic",
            warehouse.MainWarehouseId ?? 0,
            _context.MainWarehouses.Where(main => main.Id == warehouse.MainWarehouseId).Select(main => main.Name).FirstOrDefault() ?? warehouse.Name ?? "-");

    private static string? NormalizePriceGroup(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "minimum" or "min" or "lowest" => "minimum",
        "middle" or "mid" => "middle",
        "basic" or "normal" or "regular" => "basic",
        _ => null
    };

    private IQueryable<object> EmployeeSummary(IQueryable<Order> query) =>
        from order in query
        join employee in _context.Employees.AsNoTracking() on order.ApplicationUserId equals employee.ApplicationUserId
        where employee.IsActive && !employee.IsDeleted
        group order by new { employee.Id, employee.ApplicationUserId, Name = employee.DisplayName ?? employee.Name } into rows
        select new
        {
            employeeId = rows.Key.Id,
            userId = rows.Key.ApplicationUserId,
            employeeName = rows.Key.Name,
            count = rows.Count(),
            totalAmount = rows.Sum(order => order.TotalPrice)
        };

    private async Task<IActionResult> RatingDetails(IQueryable<Order> query, RatingOrderFilter filter, CancellationToken ct)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize ?? 50, 1, 200);
        var totalItems = await query.CountAsync(ct);
        var items = await query.OrderByDescending(order => order.InstantAddedDate ?? order.CreatedDate).ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(order => new
            {
                order.Id, order.CustomerName, order.TelephoneNumber, order.Country, order.State, order.OrderStatus,
                statusName = OrderStatusCodes.GetDisplayName(order.OrderStatus), order.TotalPrice, order.ApplicationUserId,
                employeeName = _context.Employees.Where(employee => employee.ApplicationUserId == order.ApplicationUserId).Select(employee => employee.DisplayName ?? employee.Name).FirstOrDefault(),
                order.ManufacturingCompanyId, createdAt = order.InstantAddedDate ?? order.CreatedDate
            }).ToListAsync(ct);
        return Ok(new { success = true, items, currentPage = page, pageSize, totalItems });
    }

    private static (DateTime Start, DateTime End) RatingRange(DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue && !endDate.HasValue)
        {
            var now = DateTime.Now;
            var day = now.TimeOfDay < TimeSpan.FromHours(10.5) ? now.Date.AddDays(-1) : now.Date;
            return (day.AddHours(10.5), day.AddDays(1).AddHours(10.5));
        }
        var start = (startDate ?? endDate)!.Value.Date.AddHours(10.5);
        var end = (endDate ?? startDate)!.Value.Date.AddHours(10.5);
        if (end <= start) end = start.AddDays(1);
        return (start, end);
    }

    private static readonly int[] DeliveredStatuses = [OrderStatusCodes.Delivered, OrderStatusCodes.BalanceUpdated, OrderStatusCodes.Paid];
    private static readonly int[] IncompleteStatuses = [OrderStatusCodes.Incomplete, OrderStatusCodes.IncompleteStage1, OrderStatusCodes.IncompleteStage2, OrderStatusCodes.IncompleteStage3, OrderStatusCodes.IncompleteStage4, OrderStatusCodes.IncompleteStage5, OrderStatusCodes.IncompleteStage6];
}

public record EmployeeRatingDto(int Id, int EmployeeId, string? EmployeeName, int Score, string? Feedback, string RatedByUserId, DateTime RatedAt);
public record SubmitRatingRequest(int EmployeeId, int Score, string? Feedback);
public record RatingOrderFilter(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    DateTime? StartDay = null,
    DateTime? EndDay = null,
    string? EmployeeId = null,
    string? EmployeeUserId = null,
    int? CountryId = null,
    int? CountyId = null,
    int? OrderSourceId = null,
    bool? GenderId = null,
    int? StoreId = null,
    int? MainWarehouseId = null,
    int? WarehouseId = null,
    int? DeliveryCompanyId = null,
    string? PriceGroup = null,
    bool? WorkShift = null,
    bool? FromComments = null,
    bool? IsFixed = null,
    int Page = 1,
    int? PageSize = 50);

public sealed record SalesIndicatorRatingRow(
    int OrderId,
    string EmployeeId,
    string EmployeeName,
    string CustomerName,
    string TelephoneNumber,
    decimal TotalPrice,
    int Country,
    DateTime CreatedAt,
    string PriceGroup,
    int MainWarehouseId,
    string ProductName);
