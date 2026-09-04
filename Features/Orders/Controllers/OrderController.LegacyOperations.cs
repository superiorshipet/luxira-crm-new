using System.Security.Claims;
using System.Text.Json;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

public partial class OrderController
{
    private const string EmployeeErrorSharePrefix = "EmployeeErrorShareNotification:";
    private const string BankTransferFollowUpAdminPrefix = "BankTransferFollowUpAdminNotification:";
    private const string OperationalNotificationPrefix = "OperationalOrderNotification:";

    [HttpGet("/Order/GetOwnOrderMissedStatusAlerts")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetOwnOrderMissedStatusAlerts([FromQuery] int take = 12, CancellationToken ct = default)
    {
        var userId = User.GetUserId() ?? string.Empty;
        take = Math.Clamp(take, 1, 30);
        var eligible = OrderStatusCodes.FailureStatuses.Concat([OrderStatusCodes.Prepared, OrderStatusCodes.InDelivery, OrderStatusCodes.Delivered]).Distinct().ToArray();
        var cutoff = IstanbulTimeHelper.Now.AddDays(-3);
        var rows = await _context.Orders.AsNoTracking().Where(order => !order.IsHidden && order.ApplicationUserId == userId && eligible.Contains(order.OrderStatus) && order.LastEditedDate >= cutoff)
            .OrderByDescending(order => order.LastEditedDate).ThenByDescending(order => order.Id).Take(take)
            .Select(order => new { order.Id, order.ExternalOrderId, order.OrderStatus, order.LastEditedDate, order.CustomerName, order.TelephoneNumber, order.SecondTelephoneNumber, order.TotalPrice, order.Country, order.State, order.SourceName }).ToListAsync(ct);
        return Ok(new { success = true, alerts = rows.Select(order => new { EventId = $"catchup:{order.Id}:{order.LastEditedDate:O}", OrderId = order.Id, ShipmentCode = order.ExternalOrderId ?? order.Id, StatusVersion = order.LastEditedDate, AlertType = order.OrderStatus == OrderStatusCodes.Delivered ? "delivered" : OrderStatusCodes.FailureStatuses.Contains(order.OrderStatus) ? "failed" : order.OrderStatus == OrderStatusCodes.Prepared ? "preparing" : "out-for-delivery", order.OrderStatus, order.CustomerName, order.TelephoneNumber, order.SecondTelephoneNumber, order.TotalPrice, order.Country, City = order.State, order.SourceName, IsCatchUp = true }) });
    }

    [HttpGet("/Order/GetFailedDeliveryTeamShiftReminder")]
    [Authorize(Roles = "FollowUpDepartment,TeamLeader,Team Leader")]
    public async Task<IActionResult> GetFailedDeliveryTeamShiftReminder(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var employee = await _context.Employees.AsNoTracking().Where(item => item.ApplicationUserId == userId && item.IsActive && !item.IsDeleted).Select(item => new { item.Id, Name = item.DisplayName ?? item.Name }).FirstOrDefaultAsync(ct);
        if (employee is null) return Ok(new { success = true, shouldShow = false, reason = "employee-not-found" });
        var shift = await _context.EmployeeWorkShifts.AsNoTracking().Where(item => item.EmployeeId == employee.Id && item.IsActive).OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(ct);
        if (shift is null) return Ok(new { success = true, shouldShow = false, reason = "shift-not-found" });
        var now = IstanbulTimeHelper.Now;
        var start = now.Date + shift.ShiftStartTime; var end = now.Date + shift.ShiftEndTime; if (end <= start) end = end.AddDays(1); if (now < start && shift.ShiftEndTime <= shift.ShiftStartTime) { start = start.AddDays(-1); end = end.AddDays(-1); }
        var isStartWindow = now >= start && now < start.AddHours(2); var isEndWindow = now >= end.AddHours(-2) && now < end;
        var count = await _context.Orders.AsNoTracking().CountAsync(order => !order.IsHidden && OrderStatusCodes.FailureStatuses.Contains(order.OrderStatus), ct);
        return Ok(new { success = true, shouldShow = count > 0 && (isStartWindow || isEndWindow), reminderKey = $"failed:{employee.Id}:{start:yyyyMMddHHmm}:{(isEndWindow ? "end" : "start")}", employee.Name, count, shiftStartAt = start, shiftEndAt = end });
    }

    [HttpGet("/Order/GetCreateOrderCampaigns")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetCreateOrderCampaigns([FromQuery] int? countryId, [FromQuery] int? manufacturingCompanyId, CancellationToken ct)
    {
        if (!countryId.HasValue || !manufacturingCompanyId.HasValue) return Ok(new { success = true, items = Array.Empty<object>() });
        var items = await _context.AdvertisingCampaigns.AsNoTracking().Where(item => item.IsActive && item.Country == countryId && item.ManufacturingCompanyId == manufacturingCompanyId)
            .OrderByDescending(item => item.CreatedAt).Select(item => new { item.Id, name = item.Name ?? "إعلان", item.ImageUrl, item.ManufacturingCompanyId }).ToListAsync(ct);
        return Ok(new { success = true, countryId, manufacturingCompanyId, items });
    }

    [HttpPost("/Order/Edit")]
    [Authorize(Roles = "Admin,Administrator,CallCenter,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> EditLegacy([FromForm] LegacyOrderEditRequest request, CancellationToken ct)
    {
        var order = await _context.Orders.Include(item => item.OrderWarehouses).FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (order is null) return NotFound();
        if (request.CustomerDeliveryPrice < 0) return BadRequest(new { message = "سعر التوصيل للعميل لا يمكن أن يكون أقل من صفر." });
        var editNumber = await _context.OrderEditHistories.Where(item => item.OrderId == order.Id).MaxAsync(item => (int?)item.EditNumber, ct) ?? 0;
        _context.OrderEditHistories.Add(new OrderEditHistory { OrderId = order.Id, EditNumber = editNumber + 1, Country = order.Country, State = order.State ?? string.Empty, OrderSource = order.OrderSource, SourceName = order.SourceName, ManufacturingCompanyId = order.ManufacturingCompanyId, DeliveryCompanyId = order.DeliveryCompanyId, TelephoneNumber = order.TelephoneNumber, SecondTelephoneNumber = order.SecondTelephoneNumber, CustomerName = order.CustomerName, Notes = order.Notes, Address = order.Address, CreatedDate = order.CreatedDate, LastEditedDate = order.LastEditedDate, FixedOrderDate = order.FixedOrderDate, InstantAddedDate = order.InstantAddedDate, OrderStatus = order.OrderStatus, TotalPrice = order.TotalPrice, ExternalOrderId = order.ExternalOrderId, ApplicationUserId = order.ApplicationUserId ?? string.Empty, FromComments = order.FromComments, Gender = order.Gender, IsPaid = order.IsPaid, Editedby = order.Editedby, CampaignId = order.CampaignId, DeliveryPrice = order.DeliveryPrice, Chaturl = order.Chaturl });
        order.Country = request.Country; order.State = request.State; order.OrderSource = request.OrderSource; order.SourceName = request.SourceName; order.ManufacturingCompanyId = request.ManufacturingCompanyId; order.DeliveryCompanyId = request.DeliveryCompanyId; order.TelephoneNumber = request.TelephoneNumber.Trim(); order.SecondTelephoneNumber = request.SecondTelephoneNumber?.Trim(); order.CustomerName = request.CustomerName.Trim(); order.Notes = request.Notes?.Trim(); order.Address = request.Address.Trim(); order.TotalPrice = request.TotalPrice; order.DeliveryPrice = request.DeliveryPrice; order.CustomerDeliveryPrice = request.CustomerDeliveryPrice; order.Chaturl = request.ChatUrl; order.CampaignId = request.CampaignId; order.Editedby = User.GetUserId(); order.LastEditedDate = IstanbulTimeHelper.Now;
        await _context.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId = order.Id, reason = "order_edited" }, ct);
        return Ok(new { success = true, orderId = order.Id });
    }

    [HttpGet("/Order/GetFailedDeliveryReminderOrders")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public Task<IActionResult> GetFailedDeliveryReminderOrders([FromQuery] int offset = 0, [FromQuery] int take = 300, CancellationToken ct = default) => ReminderOrders(true, offset, take, ct);

    [HttpGet("/Order/GetIncompleteOrdersNotificationCount")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetIncompleteOrdersNotificationCount(CancellationToken ct)
    {
        var statuses = new[] { OrderStatusCodes.New, OrderStatusCodes.WaitingForProcessing, OrderStatusCodes.Processed };
        var count = await _context.Orders.AsNoTracking().CountAsync(order => !order.IsHidden && statuses.Contains(order.OrderStatus), ct);
        return Ok(new { success = true, count });
    }

    [HttpGet("/Order/GetIncompleteOrderReminderOrders")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public Task<IActionResult> GetIncompleteOrderReminderOrders([FromQuery] int offset = 0, [FromQuery] int take = 300, CancellationToken ct = default) => ReminderOrders(false, offset, take, ct);

    [HttpPost("/Order/CheckCustomerContactShiftSummary")]
    [Authorize(Roles = "FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> CheckCustomerContactShiftSummary(CancellationToken ct)
    {
        var userId = User.GetUserId(); var now = IstanbulTimeHelper.Now;
        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.ApplicationUserId == userId, ct);
        if (employee is null) return Ok(new { success = true, shouldShow = false });
        var shift = await _context.EmployeeWorkShifts.AsNoTracking().Where(item => item.EmployeeId == employee.Id && item.IsActive).OrderByDescending(item => item.CreatedAt).FirstOrDefaultAsync(ct);
        if (shift is null) return Ok(new { success = true, shouldShow = false });
        var start = now.Date + shift.ShiftStartTime; var end = now.Date + shift.ShiftEndTime; if (end <= start) end = end.AddDays(1);
        var contacted = await _context.OrderFollowUpRequests.AsNoTracking().CountAsync(item => item.CreatedByUserId == userId && item.CreatedAt >= start && item.CreatedAt < end && item.ImagePath != null, ct);
        return Ok(new { success = true, shouldShow = now >= end.AddMinutes(-30) && now < end, contactedCount = contacted, shiftStartAt = start, shiftEndAt = end });
    }

    [HttpPost("/Order/TransferOrderWarehouse")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector,CallCenter")]
    public async Task<IActionResult> TransferOrderWarehouse([FromForm] int[] orderIds, [FromForm] int newDeliveryCompanyId, CancellationToken ct)
    {
        orderIds = orderIds.Distinct().ToArray();
        if (orderIds.Length == 0) return BadRequest(new { success = false, message = "لم يتم تحديد أي طلب." });
        var company = await _context.DeliveryCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == newDeliveryCompanyId && item.IsShown, ct);
        if (company is null) return NotFound();
        var orders = await _context.Orders
            .Include(item => item.OrderWarehouses)
            .ThenInclude(item => item.Warehouse)
            .Where(item => orderIds.Contains(item.Id))
            .ToListAsync(ct);
        if (orders.Count != orderIds.Length) return NotFound(new { success = false, message = "تعذر العثور على بعض الطلبات المحددة." });

        if (User.IsInRole("CallCenter"))
        {
            var storeIds = orders.Where(item => item.ManufacturingCompanyId.HasValue).Select(item => item.ManufacturingCompanyId!.Value).Distinct().ToArray();
            if (orders.Any(item => !item.ManufacturingCompanyId.HasValue)) return BadRequest(new { success = false, message = "لا يوجد متجر مرتبط بأحد الطلبات." });
            var allowedStores = await _context.StoreDeliveryCompanyAssignments.AsNoTracking()
                .Where(item => storeIds.Contains(item.ManufacturingCompanyId) && !item.IsManualTransfer && item.DeliveryCompanyId == newDeliveryCompanyId)
                .Select(item => item.ManufacturingCompanyId).Distinct().ToListAsync(ct);
            if (allowedStores.Count != storeIds.Length) return Forbid();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        foreach (var order in orders)
        {
            var price = await _context.DeliveryCompanyPrices.AsNoTracking().Where(item => item.DeliveryCompanyId == newDeliveryCompanyId && item.Country == order.Country && (item.City == order.State || item.City == null)).OrderByDescending(item => item.City != null).Select(item => (decimal?)item.Price).FirstOrDefaultAsync(ct);
            foreach (var line in order.OrderWarehouses.ToList())
            {
                var source = line.Warehouse;
                if (source is null || source.DeliveryCompanyId != order.DeliveryCompanyId || source.DeliveryCompanyId == newDeliveryCompanyId) continue;
                if (source.Amount < line.Amount)
                {
                    await transaction.RollbackAsync(ct);
                    return BadRequest(new { success = false, message = $"الكمية المتاحة في مخزن {source.Name} لا تكفي لنقل الطلب {order.Id}." });
                }

                var target = await _context.Warehouses.FirstOrDefaultAsync(item =>
                    item.DeliveryCompanyId == newDeliveryCompanyId &&
                    item.SubWarehouseId == source.SubWarehouseId &&
                    item.ManufacturingCompanyId == source.ManufacturingCompanyId, ct);
                if (target is null)
                {
                    target = new Warehouse
                    {
                        Name = source.Name,
                        Price = source.Price,
                        UnchangingAmount = source.UnchangingAmount,
                        Amount = 0,
                        ReservedAmount = 0,
                        DeliveryCompanyId = newDeliveryCompanyId,
                        ManufacturingCompanyId = source.ManufacturingCompanyId,
                        DateAdded = IstanbulTimeHelper.Now,
                        DateUpdated = IstanbulTimeHelper.Now,
                        MainWarehouseId = source.MainWarehouseId,
                        Countries = order.Country,
                        City = order.State,
                        SubWarehouseId = source.SubWarehouseId,
                        IsShown = source.IsShown
                    };
                    _context.Warehouses.Add(target);
                    await _context.SaveChangesAsync(ct);
                }

                source.Amount -= line.Amount;
                source.DateUpdated = IstanbulTimeHelper.Now;
                target.Amount += line.Amount;
                target.DateUpdated = IstanbulTimeHelper.Now;
                var replacement = await _context.OrderWarehouses.FindAsync([order.Id, target.Id], ct);
                if (replacement is null)
                {
                    replacement = new OrderWarehouse { OrderId = order.Id, WarehouseId = target.Id, Amount = line.Amount, UnitPrice = line.UnitPrice };
                    _context.OrderWarehouses.Add(replacement);
                }
                else
                {
                    replacement.Amount += line.Amount;
                    replacement.UnitPrice ??= line.UnitPrice;
                }
                _context.OrderWarehouses.Remove(line);
            }
            order.DeliveryCompanyId = newDeliveryCompanyId; order.DeliveryPrice = price ?? 0; order.LastEditedDate = IstanbulTimeHelper.Now; order.Editedby = User.GetUserId();
        }
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderIds = orders.Select(item => item.Id), reason = "delivery_company_updated" }, ct);
        return Ok(new { success = true, updatedCount = orders.Count });
    }

    [HttpGet("/Order/OrdersInvoice")]
    [Authorize(Roles = "Admin,Administrator,Accountant,DeliveryCompany,DeliveryRepresentative")]
    public async Task<IActionResult> OrdersInvoice([FromQuery] int? storeId, [FromQuery] int page = 1, [FromQuery] int pagesize = 10, [FromQuery] int? deliveryCompanyIdFilter = null, [FromQuery] string? search = null, [FromQuery] DateTime? startDay = null, [FromQuery] DateTime? endDay = null, [FromQuery] int? countryId = null, CancellationToken ct = default)
    {
        var query = _context.OrderReports.AsNoTracking().Where(report => report.DeliveryCompanyId != null && report.OrderStatus == OrderStatusCodes.Prepared);
        if (deliveryCompanyIdFilter.HasValue) query = query.Where(report => report.DeliveryCompanyId == deliveryCompanyIdFilter);
        if (countryId.HasValue) query = query.Where(report => report.Country == countryId);
        if (startDay.HasValue) query = query.Where(report => report.GeneratedTime >= startDay); if (endDay.HasValue) query = query.Where(report => report.GeneratedTime <= endDay);
        if (!string.IsNullOrWhiteSpace(search) && int.TryParse(search, out var id)) query = query.Where(report => report.Id == id);
        var total = await query.CountAsync(ct); page = Math.Max(1, page); pagesize = Math.Clamp(pagesize, 1, 200);
        var items = await (from report in query orderby report.GeneratedTime descending join company in _context.DeliveryCompanies.AsNoTracking() on report.DeliveryCompanyId equals company.Id select new { report.Id, report.GeneratedTime, report.TotalAmount, report.Country, deliveryCompanyName = company.Name }).Skip((page - 1) * pagesize).Take(pagesize).ToListAsync(ct);
        return Ok(new { items, currentPage = page, pageSize = pagesize, totalItems = total });
    }

    private async Task<IActionResult> ReminderOrders(bool failed, int offset, int take, CancellationToken ct)
    {
        offset = Math.Max(0, offset); take = Math.Clamp(take, 1, 500);
        var statuses = failed ? OrderStatusCodes.FailureStatuses : [OrderStatusCodes.New, OrderStatusCodes.WaitingForProcessing, OrderStatusCodes.Processed];
        var query = _context.Orders.AsNoTracking().Where(order => !order.IsHidden && statuses.Contains(order.OrderStatus));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(order => order.LastEditedDate ?? order.CreatedDate).Skip(offset).Take(take).Select(order => new { order.Id, shipmentCode = order.ExternalOrderId ?? order.Id, order.CustomerName, order.TelephoneNumber, order.SecondTelephoneNumber, order.Country, order.State, order.TotalPrice, order.OrderStatus, order.Chaturl }).ToListAsync(ct);
        return Ok(new { success = true, items, totalCount = total, nextOffset = offset + items.Count, hasMore = offset + items.Count < total });
    }
}

public sealed class LegacyOrderEditRequest
{
    public int Id { get; set; } public int Country { get; set; } public string? State { get; set; } public int OrderSource { get; set; } public string? SourceName { get; set; }
    public int? ManufacturingCompanyId { get; set; } public int DeliveryCompanyId { get; set; } public string TelephoneNumber { get; set; } = string.Empty; public string? SecondTelephoneNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty; public string? Notes { get; set; } public string Address { get; set; } = string.Empty; public decimal TotalPrice { get; set; }
    public decimal DeliveryPrice { get; set; } public decimal CustomerDeliveryPrice { get; set; } public string? ChatUrl { get; set; } public int? CampaignId { get; set; }
}
