using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,CallCenter,FollowUpDepartment,TeamLeader,ExecutiveDirector")]
[Route("api/v1/orders/prepare-for-delivery")]
[Route("PrepareForDelivery")]
public sealed class PrepareForDeliveryController(
    OrderService orderService,
    ApplicationDbContext context,
    IHubContext<OrderHub> hub) : ControllerBase
{
    [HttpGet]
    [HttpGet("GetOrders")]
    [HttpGet("Index")]
    [HttpPost("Index")]
    public async Task<ActionResult<OrderListResult>> GetPreparationOrders([FromQuery] OrderFilterRequest filter, CancellationToken ct)
    {
        var result = await orderService.GetOrdersAsync(filter with { Status = OrderStatusCodes.New }, ct);
        return Ok(result);
    }

    [HttpGet("GetShiftEndPendingDownloadReminder")]
    public async Task<IActionResult> GetShiftEndPendingDownloadReminder(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var query = VisibleQueue().Where(order => order.OrderStatus == OrderStatusCodes.New);
        if (!HasFullAccess()) query = query.Where(order => order.ApplicationUserId == userId);
        var pendingCount = await query.CountAsync(ct);
        return Ok(new { success = true, shouldRemind = pendingCount > 0, pendingCount });
    }

    [HttpGet("GetPfdVisibilityFilterOptions")]
    public async Task<IActionResult> GetPfdVisibilityFilterOptions(CancellationToken ct)
    {
        var companies = await context.DeliveryCompanies.AsNoTracking()
            .Where(company => company.IsShown && company.ShowInPrepareForDelivery)
            .OrderBy(company => company.Name)
            .Select(company => new { company.Id, company.Name, company.Country }).ToListAsync(ct);
        var countries = companies.Select(company => company.Country).Distinct().Order().ToArray();
        return Ok(new { success = true, countries, deliveryCompanies = companies });
    }

    [HttpGet("GetPageBackgroundData")]
    public async Task<IActionResult> GetPageBackgroundData(CancellationToken ct)
    {
        var queue = VisibleQueue().Where(order => order.OrderStatus == OrderStatusCodes.New);
        var counts = await queue.GroupBy(_ => 1).Select(group => new
        {
            total = group.Count(), delayed = group.Count(order => order.IsDelayed),
            bankTransfers = group.Count(order => order.IsPaid), employees = group.Select(order => order.ApplicationUserId).Distinct().Count()
        }).FirstOrDefaultAsync(ct);
        return Ok(new { success = true, counts = counts ?? new { total = 0, delayed = 0, bankTransfers = 0, employees = 0 } });
    }

    [HttpGet("GetOrderData")]
    public async Task<IActionResult> GetOrderData(int orderId, CancellationToken ct)
    {
        var order = await VisibleQueue().AsNoTracking().Where(item => item.Id == orderId).Select(item => new
        {
            item.Id, item.ExternalOrderId, item.CustomerName, item.TelephoneNumber, item.SecondTelephoneNumber,
            item.Country, item.State, item.Address, item.TotalPrice, item.IsPaid, item.IsDelayed, item.OrderStatus,
            item.ApplicationUserId, item.DeliveryCompanyId,
            DeliveryCompanyName = item.DeliveryCompany != null ? item.DeliveryCompany.Name : string.Empty,
            Warehouses = item.OrderWarehouses.Select(row => new { row.WarehouseId, row.Amount, row.UnitPrice })
        }).FirstOrDefaultAsync(ct);
        return order is null ? NotFound(new { success = false, message = "الطلب غير موجود" }) : Ok(new { success = true, order });
    }

    [HttpGet("GetEmployeesWithOrdersForDropdown")]
    public async Task<IActionResult> GetEmployeesWithOrdersForDropdown(int? countryId, int? storeId, int? deliveryCompanyId, CancellationToken ct)
    {
        var query = FilterQueue(VisibleQueue().Where(order => order.OrderStatus == OrderStatusCodes.New), countryId, storeId, deliveryCompanyId, null);
        var userIds = query.Where(order => order.ApplicationUserId != null).Select(order => order.ApplicationUserId!).Distinct();
        var employees = await context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId != null && userIds.Contains(employee.ApplicationUserId))
            .OrderBy(employee => employee.DisplayName ?? employee.Name)
            .Select(employee => new { employee.Id, userId = employee.ApplicationUserId, name = employee.DisplayName ?? employee.Name, employee.ImageUrl }).ToListAsync(ct);
        return Ok(new { success = true, employees });
    }

    [HttpGet("GetFilteredQueue")]
    public async Task<IActionResult> GetFilteredQueue(
        int? countryId, int? storeId, int? deliveryCompanyId, string? search, string? employeeId,
        int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var query = FilterQueue(VisibleQueue().Where(order => order.OrderStatus == OrderStatusCodes.New), countryId, storeId, deliveryCompanyId, search);
        if (!string.IsNullOrWhiteSpace(employeeId)) query = query.Where(order => order.ApplicationUserId == employeeId);
        var total = await query.CountAsync(ct);
        var rows = await query.AsNoTracking().OrderByDescending(order => order.IsDelayed).ThenBy(order => order.CreatedDate)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 500)).Select(order => new
            {
                order.Id, order.ExternalOrderId, order.CustomerName, order.TelephoneNumber, order.Country, order.State,
                order.TotalPrice, order.IsPaid, order.IsDelayed, order.CreatedDate, order.ApplicationUserId,
                DeliveryCompanyName = order.DeliveryCompany != null ? order.DeliveryCompany.Name : string.Empty
            }).ToListAsync(ct);
        return Ok(new { success = true, total, rows });
    }

    [HttpGet("DebugEmployeeOrders")]
    public async Task<IActionResult> DebugEmployeeOrders(string employeeId, int? countryId, int? storeId, int? deliveryCompanyId, string? search, CancellationToken ct)
    {
        var query = FilterQueue(VisibleQueue().Where(order => order.OrderStatus == OrderStatusCodes.New && order.ApplicationUserId == employeeId), countryId, storeId, deliveryCompanyId, search);
        return Ok(new { success = true, employeeId, count = await query.CountAsync(ct), orderIds = await query.Select(order => order.Id).Take(500).ToListAsync(ct) });
    }

    [HttpGet("GetRemainingDownloadsCount")]
    public async Task<IActionResult> GetRemainingDownloadsCount(CancellationToken ct)
    {
        var query = VisibleQueue().Where(order => order.OrderStatus == OrderStatusCodes.New);
        if (!HasFullAccess())
        {
            var userId = User.GetUserId() ?? string.Empty;
            query = query.Where(order => order.ApplicationUserId == userId);
        }
        var count = await query.CountAsync(ct);
        return Ok(new { success = true, count, remainingCount = count });
    }

    [HttpPost("SubmitOrder")]
    public async Task<IActionResult> SubmitOrder([FromForm] int orderId, [FromForm] string? pfdDownloadSessionId, [FromForm] int? pfdDownloadElapsedMs, CancellationToken ct)
    {
        var order = await VisibleQueue().Include(item => item.DeliveryCompany).FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return Ok(new { success = false, message = "الطلب غير موجود" });
        var userId = User.GetUserId() ?? string.Empty;
        if (!HasFullAccess() && order.ApplicationUserId != userId) return Ok(new { success = false, message = "لا يمكنك الوصول لهذا الطلب" });
        if (order.OrderStatus != OrderStatusCodes.New) return Ok(new { success = false, message = "حالة الطلب لا تسمح بهذا الإجراء" });
        var hasBlockingReport = await context.OrderPosts.AsNoTracking().AnyAsync(post => post.OrderId == orderId && post.Type == 0, ct);
        if (hasBlockingReport) return Ok(new { success = false, message = "يوجد تبليغ مشكلة مفتوح على الطلب" });
        var pendingTransfer = await context.OrderStatusHistories.AsNoTracking().AnyAsync(history => history.OrderId == orderId && history.Reason == "BankTransferPendingApproval" &&
            !context.OrderStatusHistories.Any(decision => decision.OrderId == orderId && decision.Id > history.Id && decision.Reason == "BankTransferApproved"), ct);
        if (pendingTransfer) return Ok(new { success = false, message = "الحوالة البنكية لهذا الطلب ما زالت قيد الاعتماد، ولا يمكن تنزيل الطلب قبل اعتمادها." });
        if (order.CamexTrackingNumber.HasValue || !string.IsNullOrWhiteSpace(order.SandoogReasonCode))
            return Ok(new { success = false, message = "الطلب مرتبط بشركة توصيل آلية ولا يمكن تنزيله يدوياً." });

        var now = IstanbulTimeHelper.Now;
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        context.OrderStatusHistories.AddRange(
            new OrderStatusHistory { OrderId = orderId, Status = OrderStatusCodes.Prepared, CreatedAt = now, ApplicationUserId = userId, Reason = "Prepared for delivery" },
            new OrderStatusHistory { OrderId = orderId, Status = OrderStatusCodes.InDelivery, CreatedAt = now.AddMilliseconds(1), ApplicationUserId = userId, Reason = BuildTimingReason(pfdDownloadSessionId, pfdDownloadElapsedMs) });
        var report = new OrderReport
        {
            GeneratedTime = now, TotalAmount = order.TotalPrice, Country = order.Country,
            DeliveryCompanyId = order.DeliveryCompanyId, OrderStatus = OrderStatusCodes.Prepared,
            ReportOrders = [new OrderReportOrder { OrderId = orderId }]
        };
        context.OrderReports.Add(report);
        order.OrderStatus = OrderStatusCodes.InDelivery;
        order.LastEditedDate = now;
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await hub.Clients.All.SendAsync("OrderStatusUpdated", new { OrderId = orderId, Status = OrderStatusCodes.InDelivery, StatusPhrase = OrderStatusCodes.GetDisplayName(OrderStatusCodes.InDelivery) }, ct);
        return Ok(new { success = true, orderId, status = OrderStatusCodes.InDelivery, reportId = report.Id });
    }

    [HttpPost("scan")]
    [HttpPost("ScanBarcode")]
    public Task<IActionResult> ScanBarcode([FromBody] ScanBarcodeRequest request, CancellationToken ct) => SubmitOrder(request.OrderId, request.Barcode, null, ct);

    [HttpGet("GetDownloadTimingSummary")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,TeamLeader,ExecutiveDirector")]
    public async Task<IActionResult> GetDownloadTimingSummary(DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        var start = fromDate?.Date ?? IstanbulTimeHelper.Now.Date;
        var end = toDate?.Date.AddDays(1) ?? start.AddDays(1);
        var rows = await context.OrderStatusHistories.AsNoTracking()
            .Where(history => history.Status == OrderStatusCodes.InDelivery && history.CreatedAt >= start && history.CreatedAt < end && history.Reason != null && history.Reason.StartsWith("PfdDownload:"))
            .GroupBy(history => history.ApplicationUserId).Select(group => new { employeeUserId = group.Key, submittedCount = group.Count(), firstAt = group.Min(item => item.CreatedAt), lastAt = group.Max(item => item.CreatedAt) })
            .ToListAsync(ct);
        return Ok(new { success = true, rows });
    }

    [HttpPost("SetDelayed")]
    public async Task<IActionResult> SetDelayed([FromForm] int orderId, [FromForm] bool isDelayed, CancellationToken ct)
    {
        var changed = await VisibleQueue().Where(order => order.Id == orderId).ExecuteUpdateAsync(setters => setters
            .SetProperty(order => order.IsDelayed, isDelayed).SetProperty(order => order.LastEditedDate, IstanbulTimeHelper.Now), ct);
        return changed == 0 ? NotFound(new { success = false }) : Ok(new { success = true, orderId, isDelayed });
    }

    [HttpGet("GetRecentlySubmitted")]
    public Task<IActionResult> GetRecentlySubmitted(CancellationToken ct) => RecentlySubmitted(null, ct);

    [HttpGet("SearchRecentlySubmitted")]
    public Task<IActionResult> SearchRecentlySubmitted(string? search, CancellationToken ct) => RecentlySubmitted(search, ct);

    [HttpGet("GetLastSubmittedOrder")]
    public async Task<IActionResult> GetLastSubmittedOrder(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var item = await context.OrderStatusHistories.AsNoTracking().Where(history => history.ApplicationUserId == userId && history.Status == OrderStatusCodes.InDelivery)
            .OrderByDescending(history => history.Id).Join(context.Orders.AsNoTracking(), history => history.OrderId, order => order.Id,
                (history, order) => new { order.Id, order.CustomerName, order.TelephoneNumber, history.CreatedAt }).FirstOrDefaultAsync(ct);
        return Ok(new { success = true, item });
    }

    private async Task<IActionResult> RecentlySubmitted(string? search, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? string.Empty;
        var historyQuery = context.OrderStatusHistories.AsNoTracking().Where(history => history.ApplicationUserId == userId && history.Status == OrderStatusCodes.InDelivery);
        var query = historyQuery.Join(context.Orders.AsNoTracking(), history => history.OrderId, order => order.Id, (history, order) => new { history, order });
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(row => row.order.CustomerName.Contains(search) || row.order.TelephoneNumber.Contains(search) || row.order.Id.ToString() == search);
        var rows = await query.OrderByDescending(row => row.history.Id).Take(50).Select(row => new { row.order.Id, row.order.CustomerName, row.order.TelephoneNumber, submittedAt = row.history.CreatedAt }).ToListAsync(ct);
        return Ok(new { success = true, rows });
    }

    private IQueryable<Order> VisibleQueue() => context.Orders.Where(order => !order.IsHidden && context.DeliveryCompanies.Any(company => company.Id == order.DeliveryCompanyId && company.IsShown && company.ShowInPrepareForDelivery));

    private static IQueryable<Order> FilterQueue(IQueryable<Order> query, int? country, int? store, int? deliveryCompany, string? search)
    {
        if (country.HasValue) query = query.Where(order => order.Country == country.Value);
        if (store.HasValue) query = query.Where(order => order.ManufacturingCompanyId == store.Value);
        if (deliveryCompany.HasValue) query = query.Where(order => order.DeliveryCompanyId == deliveryCompany.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(order => order.CustomerName.Contains(search) || order.TelephoneNumber.Contains(search) || (order.State != null && order.State.Contains(search)) || order.Id.ToString() == search);
        return query;
    }

    private bool HasFullAccess() => User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector") || User.IsInRole("FollowUpDepartment");
    private static string BuildTimingReason(string? sessionId, int? elapsedMs) => $"PfdDownload:{sessionId ?? string.Empty}:{Math.Max(0, elapsedMs ?? 0)}";
}

public record ScanBarcodeRequest(int OrderId, string Barcode);
