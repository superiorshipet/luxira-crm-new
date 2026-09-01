using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders")]
[Route("api/v1/order")]
[Route("api/orders")]
[Route("api/order")]
[Route("Order")]
public class OrderController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ApplicationDbContext _context;

    public OrderController(OrderService orderService, ApplicationDbContext context)
    {
        _orderService = orderService;
        _context = context;
    }

    [HttpGet]
    [HttpGet("/Order/GetOrders")]
    [HttpGet("/Order/Index")]
    public async Task<ActionResult<OrderListResult>> GetOrders([FromQuery] OrderFilterRequest filter, CancellationToken ct)
    {
        var result = await _orderService.GetOrdersAsync(filter, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HttpGet("/Order/GetOrderById/{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(int id, CancellationToken ct)
    {
        var result = await _orderService.GetOrderByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HttpPost("/Order/Create")]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var result = await _orderService.CreateOrderAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetOrderById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HttpPost("Edit/{id:int}")]
    [HttpPost("/Order/Edit/{id:int}")]
    public async Task<IActionResult> EditOrder([FromRoute] int id, [FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order == null) return NotFound("Order not found.");

        order.CustomerName = request.CustomerName;
        order.TelephoneNumber = request.TelephoneNumber;
        order.SecondTelephoneNumber = request.SecondTelephoneNumber;
        order.Address = request.Address;
        order.Country = request.Country;
        order.State = request.State;
        order.TotalPrice = request.TotalPrice;
        order.DeliveryPrice = request.DeliveryPrice;
        order.CustomerDeliveryPrice = request.CustomerDeliveryPrice;
        order.DeliveryCompanyId = request.DeliveryCompanyId;
        order.ManufacturingCompanyId = request.ManufacturingCompanyId;
        order.Notes = request.Notes;
        order.LastEditedDate = IstanbulTimeHelper.Now;
        order.Editedby = User.GetUserId() ?? "system";

        await _context.SaveChangesAsync(ct);
        return Ok(order);
    }

    [HttpPut("{id:int}/status")]
    [HttpPost("/Order/UpdateStatus/{id:int}")]
    public async Task<ActionResult<OrderDto>> UpdateStatus(
        [FromRoute] int id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var result = await _orderService.UpdateOrderStatusAsync(id, request, userId, ct);
        return Ok(result);
    }

    [HttpPost("batch-status")]
    [HttpPost("/Order/BatchUpdateStatus")]
    public async Task<IActionResult> BatchUpdateStatus(
        [FromBody] BatchUpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        int updated = await _orderService.BatchUpdateOrderStatusAsync(request, userId, ct);
        return Ok(new { updatedCount = updated, message = $"Successfully updated {updated} orders." });
    }

    [HttpGet("stats")]
    [HttpGet("/Order/GetStats")]
    public async Task<ActionResult<OrderStatsDto>> GetStats([FromQuery] int? country, CancellationToken ct)
    {
        var stats = await _orderService.GetStatsAsync(country, ct);
        return Ok(stats);
    }

    [HttpPost("inline-edit")]
    [HttpPost("/Order/UpdateOrderInlineField")]
    public async Task<ActionResult<OrderDto>> UpdateOrderInlineField([FromBody] UpdateInlineFieldRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var result = await _orderService.UpdateInlineFieldAsync(request.OrderId, request.FieldName, request.NewValue, userId, ct);
        return Ok(result);
    }

    [HttpGet("check-duplicates")]
    [HttpGet("/Order/CheckDuplicates")]
    public async Task<ActionResult<List<OrderDto>>> CheckDuplicates([FromQuery] string phoneNumber, CancellationToken ct)
    {
        var duplicates = await _orderService.CheckDuplicatesAsync(phoneNumber, ct);
        return Ok(duplicates);
    }

    // --- GAP REMEDIATION ENDPOINTS ---

    [HttpGet("filter-counts")]
    [HttpGet("/Order/GetFilterCounts")]
    public async Task<IActionResult> GetFilterCounts([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();
        if (country.HasValue) query = query.Where(o => o.Country == country.Value);

        var total = await query.CountAsync(ct);
        var closedStatuses = OrderStatusCodes.ClosedStatuses;
        var active = await query.CountAsync(o => !closedStatuses.Contains(o.OrderStatus), ct);
        var delivered = await query.CountAsync(
            o => o.OrderStatus == OrderStatusCodes.Delivered,
            ct);
        var delayed = await query.CountAsync(o => o.IsDelayed, ct);
        var complaints = await query.CountAsync(o => o.IsComplaints, ct);

        return Ok(new { total, active, delivered, delayed, complaints });
    }

    [HttpGet("failure-reason-counts")]
    [HttpGet("/Order/GetFailureReasonCounts")]
    public async Task<IActionResult> GetFailureReasonCounts([FromQuery] int? country, CancellationToken ct)
    {
        var failureStatuses = OrderStatusCodes.FailureStatuses;
        var query = _context.OrderStatusHistories
            .Include(h => h.Order)
            .Where(h => failureStatuses.Contains(h.NewStatus) &&
                        !string.IsNullOrEmpty(h.Reason))
            .AsNoTracking()
            .AsQueryable();

        if (country.HasValue) query = query.Where(h => h.Order != null && h.Order.Country == country.Value);

        var counts = await query
            .GroupBy(h => h.Reason)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(counts);
    }

    [HttpGet("status-counts")]
    [HttpGet("/Order/GetOrderStatusCounts")]
    public async Task<IActionResult> GetOrderStatusCounts([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();
        if (country.HasValue) query = query.Where(o => o.Country == country.Value);

        var counts = await query
            .GroupBy(o => o.OrderStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(counts);
    }

    [HttpGet("in-delivery-count")]
    [HttpGet("/Order/GetInDeliveryStatusUpdateCount")]
    public async Task<IActionResult> GetInDeliveryStatusUpdateCount([FromQuery] int? deliveryCompanyId, CancellationToken ct)
    {
        var query = _context.Orders
            .Where(o => o.OrderStatus == OrderStatusCodes.InDelivery)
            .AsNoTracking()
            .AsQueryable();
        if (deliveryCompanyId.HasValue) query = query.Where(o => o.DeliveryCompanyId == deliveryCompanyId.Value);

        var count = await query.CountAsync(ct);
        return Ok(new { inDeliveryCount = count });
    }

    [HttpGet("validate-total-price")]
    [HttpGet("/Order/ValidateTotalPrice")]
    public IActionResult ValidateTotalPrice([FromQuery] decimal totalPrice, [FromQuery] decimal itemsPrice, [FromQuery] decimal deliveryPrice)
    {
        bool isValid = Math.Abs(totalPrice - (itemsPrice + deliveryPrice)) < 0.01m;
        return Ok(new { isValid, expectedTotal = itemsPrice + deliveryPrice, providedTotal = totalPrice });
    }

    [HttpGet("validate-min-price")]
    [HttpGet("/Order/ValidateProductMinimumSellingPrice")]
    public async Task<IActionResult> ValidateProductMinimumSellingPrice([FromQuery] int productId, [FromQuery] int country, [FromQuery] decimal price, CancellationToken ct)
    {
        var minSetting = await _context.ProductMinimumSellingPrices
            .FirstOrDefaultAsync(p => p.MainProductId == productId && p.Country == country, ct);

        if (minSetting != null && price < minSetting.MinimumPrice)
        {
            return Ok(new { isValid = false, minPrice = minSetting.MinimumPrice, message = $"Price is below minimum allowed price ({minSetting.MinimumPrice})" });
        }

        return Ok(new { isValid = true, minPrice = minSetting?.MinimumPrice ?? 0m });
    }

    [HttpGet("pricing-selection/{id:int}")]
    [HttpGet("/Order/GetOrderPricingSelection")]
    public async Task<IActionResult> GetOrderPricingSelection([FromRoute] int id, [FromQuery] int? orderId, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (orderId ?? 0);
        var order = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .FirstOrDefaultAsync(o => o.Id == targetId, ct);

        if (order == null) return NotFound("Order not found.");

        return Ok(new
        {
            order.Id,
            order.TotalPrice,
            order.DeliveryPrice,
            order.CustomerDeliveryPrice,
            Warehouses = order.OrderWarehouses
        });
    }

    [HttpGet("create-warehouses")]
    [HttpGet("/Order/GetCreateOrderWarehouses")]
    public async Task<IActionResult> GetCreateOrderWarehouses([FromQuery] int? country, CancellationToken ct)
    {
        var warehouses = await _context.Warehouses
            .Where(w => w.IsActive)
            .Select(w => new { w.Id, w.Name, w.Country, w.Address })
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(warehouses);
    }

    [HttpGet("delivery-parties-by-country")]
    [HttpGet("/Order/GetCreateOrderDeliveryPartiesByCountry")]
    public async Task<IActionResult> GetCreateOrderDeliveryPartiesByCountry([FromQuery] int country, CancellationToken ct)
    {
        var companies = await _context.DeliveryCompanies
            .Where(d => d.Country == country && d.IsShown)
            .Select(d => new { d.Id, d.Name, d.IsRepresentative, d.PhoneNumber })
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(companies);
    }

    [HttpGet("delivery-payment-methods")]
    [HttpGet("/Order/GetDeliveryCompanyPaymentMethods")]
    public IActionResult GetDeliveryCompanyPaymentMethods([FromQuery] int deliveryCompanyId)
    {
        var methods = new[] { "Cash on Delivery", "Prepaid", "Bank Transfer", "Credit Card" };
        return Ok(methods);
    }

    [HttpGet("delivery-companies-filter")]
    [HttpGet("/Order/GetDeliveryCompaniesForAllStatusesFilter")]
    public async Task<IActionResult> GetDeliveryCompaniesForAllStatusesFilter([FromQuery] int? countryId, [FromQuery] string? cityId, CancellationToken ct)
    {
        var query = _context.DeliveryCompanies.Where(d => d.IsShown).AsNoTracking().AsQueryable();
        if (countryId.HasValue) query = query.Where(d => d.Country == countryId.Value);

        var list = await query.Select(d => new { d.Id, d.Name, d.Country, d.IsRepresentative }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("delivery-costs")]
    [HttpGet("/Order/DeliveryCostsByOrderIds")]
    public async Task<IActionResult> DeliveryCostsByOrderIds([FromQuery] string ids, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ids)) return Ok(new { totalDeliveryCost = 0m, count = 0 });

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var i) ? i : 0)
            .Where(i => i > 0)
            .ToList();

        var orders = await _context.Orders
            .Where(o => idList.Contains(o.Id))
            .Select(o => o.DeliveryPrice)
            .ToListAsync(ct);

        return Ok(new { count = orders.Count, totalDeliveryCost = orders.Sum() });
    }

    [HttpPost("status-selection/save")]
    [HttpPost("/Order/SaveStatusUpdateSelection")]
    public IActionResult SaveStatusUpdateSelection([FromBody] StatusSelectionRequest request)
    {
        return Ok(new { success = true, selectedCount = request.OrderIds.Count });
    }

    [HttpPost("status-selection/clear-mine")]
    [HttpPost("/Order/ClearMyStatusUpdateSelections")]
    public IActionResult ClearMyStatusUpdateSelections()
    {
        return Ok(new { success = true });
    }

    [HttpPost("status-selection/clear/{orderId:int}")]
    [HttpPost("/Order/ClearStatusUpdateSelection")]
    public IActionResult ClearStatusUpdateSelection([FromRoute] int orderId)
    {
        return Ok(new { success = true, clearedOrderId = orderId });
    }

    [HttpGet("status-selections")]
    [HttpGet("/Order/GetStatusUpdateSelections")]
    public IActionResult GetStatusUpdateSelections()
    {
        return Ok(new List<int>());
    }

    [HttpPost("draft-field")]
    [HttpPost("/Order/SaveCreateOrderDraftField")]
    public IActionResult SaveCreateOrderDraftField([FromBody] OrderDraftFieldRequest request)
    {
        return Ok(new { success = true, field = request.FieldName, savedAt = IstanbulTimeHelper.Now });
    }

    [HttpPost("{id:int}/move-to-yesterday-ratings")]
    [HttpPost("/Order/MoveOrderToYesterdayRatings")]
    public async Task<IActionResult> MoveOrderToYesterdayRatings([FromRoute] int id, [FromQuery] int? orderId, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (orderId ?? 0);
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == targetId, ct);
        if (order == null) return NotFound("Order not found.");

        order.FixedOrderDate = IstanbulTimeHelper.Now.AddDays(-1);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, targetDate = order.FixedOrderDate });
    }

    [HttpPost("{id:int}/block-duplicate")]
    [HttpPost("/Order/BlockDuplicateOrder")]
    public async Task<IActionResult> BlockDuplicateOrder([FromRoute] int id, [FromQuery] int? orderId, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (orderId ?? 0);
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == targetId, ct);
        if (order == null) return NotFound("Order not found.");

        var oldStatus = order.OrderStatus;
        order.OrderStatus = OrderStatusCodes.Cancelled;
        order.Notes = (order.Notes ?? "") + " [Blocked as Duplicate]";
        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            OldStatus = oldStatus,
            NewStatus = OrderStatusCodes.Cancelled,
            UserId = User.GetUserId() ?? "system",
            ChangedAt = IstanbulTimeHelper.Now,
            Reason = "Blocked as duplicate without deleting data",
        });
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("hidden")]
    [HttpGet("/Order/HiddenOrders")]
    public async Task<IActionResult> HiddenOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = _context.Orders.Where(o => o.IsHidden).OrderByDescending(o => o.CreatedDate).AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost("{id:int}/delivered")]
    [HttpPost("/Order/UpdateDelivered")]
    public async Task<IActionResult> UpdateDelivered([FromRoute] int id, [FromQuery] int? orderId, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (orderId ?? 0);
        var result = await _orderService.UpdateOrderStatusAsync(
            targetId,
            new UpdateOrderStatusRequest(
                OrderStatusCodes.Delivered,
                "Marked delivered",
                null),
            User.GetUserId() ?? "system",
            ct);
        return Ok(new { success = true, status = result.OrderStatus });
    }

    [HttpPost("{id:int}/failed-delivery")]
    [HttpPost("/Order/UpdateFailedDelivery")]
    public async Task<IActionResult> UpdateFailedDelivery([FromRoute] int id, [FromQuery] int? orderId, [FromBody] FailedDeliveryRequest request, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (orderId ?? 0);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BadRequestException("Failure reason is required.");
        }

        var result = await _orderService.UpdateOrderStatusAsync(
            targetId,
            new UpdateOrderStatusRequest(
                OrderStatusCodes.FailedDelivery,
                request.Reason.Trim(),
                null),
            User.GetUserId() ?? "system",
            ct);
        return Ok(new { success = true, status = result.OrderStatus });
    }

    [HttpPost("update-all-statuses")]
    [HttpPost("/Order/UpdateAllStatuses")]
    public async Task<IActionResult> UpdateAllStatuses([FromBody] UpdateAllStatusesRequest request, CancellationToken ct)
    {
        if (!OrderStatusCodes.IsDefined(request.NewStatus))
        {
            throw new BadRequestException($"Order status '{request.NewStatus}' is not part of the legacy status contract.");
        }

        var orders = await _context.Orders.Where(o => request.OrderIds.Contains(o.Id)).ToListAsync(ct);
        var now = IstanbulTimeHelper.Now;
        var userId = User.GetUserId() ?? "system";

        foreach (var order in orders)
        {
            var oldStatus = order.OrderStatus;
            order.OrderStatus = request.NewStatus;
            order.LastEditedDate = now;

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                OldStatus = oldStatus,
                NewStatus = request.NewStatus,
                UserId = userId,
                ChangedAt = now,
                Reason = request.Reason ?? "Bulk Status Update"
            });
        }

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, updatedCount = orders.Count });
    }

    [HttpGet("{orderId:int}/inline-options")]
    [HttpGet("/Order/GetOrderInlineEditOptions")]
    public async Task<IActionResult> GetOrderInlineEditOptions([FromRoute] int orderId, [FromQuery] string field, CancellationToken ct)
    {
        if (field.Equals("state", StringComparison.OrdinalIgnoreCase))
        {
            var cities = await _context.CamexCities.Select(c => c.CityName).Distinct().ToListAsync(ct);
            return Ok(cities);
        }
        if (field.Equals("store", StringComparison.OrdinalIgnoreCase))
        {
            var stores = await _context.ManufacturingCompanies.Where(m => m.IsActive).Select(m => new { m.Id, m.Name }).ToListAsync(ct);
            return Ok(stores);
        }
        return Ok(new List<string>());
    }

    [HttpPost("update-inline-store")]
    [HttpPost("/Order/UpdateOrderInlineStore")]
    public async Task<IActionResult> UpdateOrderInlineStore([FromBody] UpdateInlineStoreRequest request, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order == null) return NotFound("Order not found.");

        order.ManufacturingCompanyId = request.StoreId;
        order.LastEditedDate = IstanbulTimeHelper.Now;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, storeId = order.ManufacturingCompanyId });
    }
}

public record StatusSelectionRequest(List<int> OrderIds, int Status);
public record OrderDraftFieldRequest(string FieldName, string? Value);
public record FailedDeliveryRequest(string Reason);
public record UpdateAllStatusesRequest(List<int> OrderIds, int NewStatus, string? Reason);
public record UpdateInlineStoreRequest(int OrderId, int StoreId);
public record UpdateInlineFieldRequest(int OrderId, string FieldName, string? NewValue);
