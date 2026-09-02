using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Infrastructure.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    private readonly LuxiraCacheService _cache;
    private readonly IHubContext<OrderHub> _hub;

    public OrderController(
        OrderService orderService,
        ApplicationDbContext context,
        LuxiraCacheService cache,
        IHubContext<OrderHub> hub)
    {
        _orderService = orderService;
        _context = context;
        _cache = cache;
        _hub = hub;
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
        var actor = OrderStatusActor.FromPrincipal(User);
        var result = await _orderService.UpdateOrderStatusAsync(id, request, actor, ct);
        return Ok(result);
    }

    [HttpPost("batch-status")]
    [HttpPost("/Order/BatchUpdateStatus")]
    public async Task<IActionResult> BatchUpdateStatus(
        [FromBody] BatchUpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var actor = OrderStatusActor.FromPrincipal(User);
        int updated = await _orderService.BatchUpdateOrderStatusAsync(request, actor, ct);
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

        var closedStatuses = OrderStatusCodes.ClosedStatuses;
        var counts = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                total = group.Count(),
                active = group.Count(order => !closedStatuses.Contains(order.OrderStatus)),
                delivered = group.Count(order => order.OrderStatus == OrderStatusCodes.Delivered),
                delayed = group.Count(order => order.IsDelayed),
                complaints = group.Count(order => order.IsComplaints),
            })
            .FirstOrDefaultAsync(ct);

        return Ok(counts ?? new { total = 0, active = 0, delivered = 0, delayed = 0, complaints = 0 });
    }

    [HttpGet("failure-reason-counts")]
    [HttpGet("/Order/GetFailureReasonCounts")]
    public async Task<IActionResult> GetFailureReasonCounts([FromQuery] int? country, CancellationToken ct)
    {
        var failureStatuses = OrderStatusCodes.FailureStatuses;
        var query = _context.OrderStatusHistories
            .Include(h => h.Order)
            .Where(h => h.Status.HasValue &&
                        failureStatuses.Contains(h.Status.Value) &&
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
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> ValidateTotalPrice(
        [FromQuery] int? country,
        [FromQuery] int? manufacturingCompanyId,
        [FromQuery] decimal? totalPrice,
        [FromQuery] bool strict = false,
        [FromQuery] decimal? itemsPrice = null,
        [FromQuery] decimal? deliveryPrice = null,
        CancellationToken ct = default)
    {
        if (country.HasValue && manufacturingCompanyId.HasValue)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Administrator") ||
                User.IsInRole("ExecutiveDirector") || User.IsInRole("FollowUpDepartment"))
            {
                return Ok(new { valid = true, bypass = true });
            }

            if (!totalPrice.HasValue || manufacturingCompanyId <= 0)
            {
                return Ok(new { valid = true, pending = true });
            }

            var minimum = await _context.CountryMinimumPrices
                .AsNoTracking()
                .Where(price => price.Country == country.Value &&
                                price.ManufacturingCompanyId == manufacturingCompanyId.Value)
                .Select(price => (decimal?)price.MinimumPriceForOffers)
                .FirstOrDefaultAsync(ct);
            if (!minimum.HasValue || totalPrice.Value >= minimum.Value)
            {
                return Ok(new { valid = true });
            }

            var message = $"لا يمكننك تنزيل طلب بأقل من الحد الأدنى {minimum.Value}";
            return strict
                ? Ok(new { valid = false, message, minimum = minimum.Value })
                : Ok(new { valid = true, warning = true, message, minimum = minimum.Value });
        }

        if (!totalPrice.HasValue || !itemsPrice.HasValue || !deliveryPrice.HasValue)
        {
            return Ok(new { isValid = true, pending = true });
        }

        var expectedTotal = itemsPrice.Value + deliveryPrice.Value;
        var isValid = Math.Abs(totalPrice.Value - expectedTotal) < 0.01m;
        return Ok(new { isValid, expectedTotal, providedTotal = totalPrice.Value });
    }

    [HttpGet("validate-min-price")]
    [HttpGet("/Order/ValidateProductMinimumSellingPrice")]
    public async Task<IActionResult> ValidateProductMinimumSellingPrice([FromQuery] int productId, [FromQuery] int country, [FromQuery] decimal price, CancellationToken ct)
    {
        var minSetting = await _context.ProductMinimumSellingPrices
            .FirstOrDefaultAsync(p => p.MainWarehouseId == productId && p.Country == country, ct);

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
            .Where(w => w.IsShown)
            .Select(w => new { w.Id, w.Name, Country = w.Countries, Address = string.Empty })
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
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetDeliveryCompanyPaymentMethods(
        [FromQuery] int deliveryCompanyId,
        CancellationToken ct)
    {
        var settings = await _context.DeliveryCompanies
            .AsNoTracking()
            .Where(company => company.Id == deliveryCompanyId)
            .Select(company => new
            {
                deliveryCompanyId = company.Id,
                companyName = company.Name,
                supportsCashPayment = company.SupportsCashPayment,
                supportsBankTransferPayment = company.SupportsBankTransferPayment,
            })
            .FirstOrDefaultAsync(ct);

        return settings is null
            ? Ok(new { success = false, message = "لم يتم العثور على شركة التوصيل المختارة." })
            : Ok(new
            {
                success = true,
                settings.deliveryCompanyId,
                settings.companyName,
                settings.supportsCashPayment,
                settings.supportsBankTransferPayment,
            });
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
    public async Task<IActionResult> SaveStatusUpdateSelection(
        [FromBody] StatusSelectionRequest request,
        CancellationToken ct)
    {
        var selections = request.OrderIds.Where(id => id > 0).Distinct().Take(5_000).ToList();
        await _cache.SetAsync(StatusSelectionKey(), selections, TimeSpan.FromHours(8), ct: ct);
        return Ok(new { success = true, selectedCount = selections.Count });
    }

    [HttpPost("status-selection/clear-mine")]
    [HttpPost("/Order/ClearMyStatusUpdateSelections")]
    public async Task<IActionResult> ClearMyStatusUpdateSelections(CancellationToken ct)
    {
        await _cache.InvalidateAsync(StatusSelectionKey(), ct);
        return Ok(new { success = true });
    }

    [HttpPost("status-selection/clear/{orderId:int}")]
    [HttpPost("/Order/ClearStatusUpdateSelection")]
    public async Task<IActionResult> ClearStatusUpdateSelection([FromRoute] int orderId, CancellationToken ct)
    {
        var key = StatusSelectionKey();
        var selections = await _cache.GetOrCreateAsync(
            key,
            _ => Task.FromResult(new List<int>()),
            TimeSpan.FromHours(8),
            ct: ct);
        if (selections.Remove(orderId))
            await _cache.SetAsync(key, selections, TimeSpan.FromHours(8), ct: ct);
        return Ok(new { success = true, clearedOrderId = orderId });
    }

    [HttpGet("status-selections")]
    [HttpGet("/Order/GetStatusUpdateSelections")]
    public async Task<IActionResult> GetStatusUpdateSelections(CancellationToken ct)
    {
        var selections = await _cache.GetOrCreateAsync(
            StatusSelectionKey(),
            _ => Task.FromResult(new List<int>()),
            TimeSpan.FromHours(8),
            ct: ct);
        return Ok(selections);
    }

    [HttpPost("draft-field")]
    [HttpPost("/Order/SaveCreateOrderDraftField")]
    public async Task<IActionResult> SaveCreateOrderDraftField(
        [FromBody] OrderDraftFieldRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FieldName) || request.FieldName.Length > 100)
            throw new BadRequestException("Invalid draft field name.");
        if (request.Value?.Length > 10_000)
            throw new BadRequestException("Draft field value is too long.");

        var key = DraftKey();
        var draft = await _cache.GetOrCreateAsync(
            key,
            _ => Task.FromResult(new Dictionary<string, string?>()),
            TimeSpan.FromDays(1),
            ct: ct);
        draft[request.FieldName.Trim()] = request.Value;
        await _cache.SetAsync(key, draft, TimeSpan.FromDays(1), ct: ct);
        return Ok(new { success = true, field = request.FieldName, savedAt = IstanbulTimeHelper.Now });
    }

    [HttpGet("draft-fields")]
    public async Task<IActionResult> GetCreateOrderDraft(CancellationToken ct)
    {
        var draft = await _cache.GetOrCreateAsync(
            DraftKey(),
            _ => Task.FromResult(new Dictionary<string, string?>()),
            TimeSpan.FromDays(1),
            ct: ct);
        return Ok(draft);
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
            Status = OrderStatusCodes.Cancelled,
            ApplicationUserId = User.GetUserId() ?? "system",
            CreatedAt = IstanbulTimeHelper.Now,
            Reason = "Blocked as duplicate without deleting data",
            Name = $"PreviousStatus:{oldStatus}",
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
            OrderStatusActor.FromPrincipal(User),
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
            OrderStatusActor.FromPrincipal(User),
            ct);
        return Ok(new { success = true, status = result.OrderStatus });
    }

    [HttpPost("update-all-statuses")]
    [HttpPost("/Order/UpdateAllStatuses")]
    public async Task<IActionResult> UpdateAllStatuses([FromBody] UpdateAllStatusesRequest request, CancellationToken ct)
    {
        var updated = await _orderService.BatchUpdateOrderStatusAsync(
            new BatchUpdateOrderStatusRequest(request.OrderIds, request.NewStatus, request.Reason, null),
            OrderStatusActor.FromPrincipal(User),
            ct);
        return Ok(new { success = true, updatedCount = updated });
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
            var stores = await _context.ManufacturingCompanies.Where(m => m.IsShown).Select(m => new { m.Id, m.Name }).ToListAsync(ct);
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

    [HttpGet("/Order/Details")]
    [HttpPost("/Order/Details")]
    public async Task<ActionResult<OrderDto>> LegacyDetails([FromQuery] int id, CancellationToken ct) =>
        Ok(await _orderService.GetOrderByIdAsync(id, ct));

    [HttpGet("/Order/DetailsPartial")]
    public async Task<ActionResult<OrderDto>> LegacyDetailsPartial([FromQuery] int id, CancellationToken ct) =>
        Ok(await _orderService.GetOrderByIdAsync(id, ct));

    [HttpPost("/Order/PostponeOrder")]
    public async Task<IActionResult> PostponeOrder(
        [FromForm] int orderId,
        [FromForm] DateTime newCreatedDate,
        CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return NotFound($"Order with ID {orderId} not found.");
        order.CreatedDate = newCreatedDate;
        var newStatus = (newCreatedDate - IstanbulTimeHelper.Now).TotalDays > 2
            ? OrderStatusCodes.Postponed
            : OrderStatusCodes.New;
        if ((order.OrderStatus == OrderStatusCodes.Postponed && newStatus == OrderStatusCodes.New) ||
            newStatus == OrderStatusCodes.Postponed)
        {
            var previousStatus = order.OrderStatus;
            order.OrderStatus = newStatus;
            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = newStatus,
                CreatedAt = IstanbulTimeHelper.Now,
                ApplicationUserId = User.GetUserId(),
                Name = $"PreviousStatus:{previousStatus}",
            });
        }
        await _context.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId, reason = "order_postponed" }, ct);
        return Ok(new { success = true });
    }

    [HttpPost("/Order/HideOrder")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> HideOrder([FromForm] int orderId, [FromForm] bool isHidden, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return NotFound($"Order with ID {orderId} not found.");
        order.IsHidden = isHidden;
        await _context.SaveChangesAsync(ct);
        var payload = new { orderId, isHidden = order.IsHidden };
        await Task.WhenAll(
            _hub.Clients.All.SendAsync("OrderHiddenStatusUpdated", payload, ct),
            _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId, reason = "hidden_status_updated" }, ct));
        return Ok($"الطلب بالمعرّف {orderId} الآن {(isHidden ? "hidden" : "unhidden")}.");
    }

    [HttpPost("/Order/SetSpecial")]
    public async Task<IActionResult> SetSpecial([FromForm] int id, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (order is null) return NotFound();
        order.IsClientSpecial = !order.IsClientSpecial;
        await _context.SaveChangesAsync(ct);
        await Task.WhenAll(
            _hub.Clients.All.SendAsync("UpdateOrderClientType", new { OrderId = id, IsClientSpecial = order.IsClientSpecial }, ct),
            _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId = id, reason = "client_type_updated" }, ct));
        return Ok(new { redirectUrl = $"/Order/Details?id={id}" });
    }

    [HttpPost("/Order/SetIsComplaints")]
    public async Task<IActionResult> SetIsComplaints([FromForm] int id, CancellationToken ct)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (order is null) return NotFound();
        order.IsComplaints = !order.IsComplaints;
        await _context.SaveChangesAsync(ct);
        await Task.WhenAll(
            _hub.Clients.All.SendAsync("UpdateOrderComplaintsType", new { OrderId = id, IsComplaints = order.IsComplaints }, ct),
            _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId = id, reason = "complaints_updated" }, ct));
        return Ok(new { redirectUrl = $"/Order/Details?id={id}" });
    }

    [HttpPost("/Order/SetBonusPaidForEmployee")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetBonusPaidForEmployee([FromBody] List<int>? orderIds, CancellationToken ct)
    {
        if (orderIds is null || orderIds.Count == 0) return BadRequest("No order IDs provided.");
        var ids = orderIds.Where(id => id > 0).Distinct().ToList();
        var affected = await _context.Orders.Where(order => ids.Contains(order.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.IsBonusPaidForEmployee, true), ct);
        return Ok(new { success = true, updatedCount = affected, message = "تم الدفع ", redirectUrl = "/Financial/Employees" });
    }

    [HttpGet("/Order/GetAllOrderIdsForEmployeeBonus")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> GetAllOrderIdsForEmployeeBonus(
        [FromQuery] string? employeeId,
        [FromQuery] bool? isEmployeebonus,
        CancellationToken ct)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(employeeId)) query = query.Where(order => order.ApplicationUserId == employeeId);
        if (isEmployeebonus.HasValue) query = query.Where(order => order.IsBonus == isEmployeebonus.Value);
        return Ok(await query.Select(order => order.Id).ToListAsync(ct));
    }

    [HttpPost("/Order/RemoveWarehouse")]
    public async Task<IActionResult> RemoveWarehouse(
        [FromForm] int orderId,
        [FromForm] int warehouseId,
        CancellationToken ct)
    {
        var order = await _context.Orders.Include(item => item.OrderWarehouses)
            .FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return NotFound();
        if (order.OrderStatus == OrderStatusCodes.Delivered)
            return Content("تم التسليم لا تستطيع التعديل عليه");
        var orderWarehouse = order.OrderWarehouses.FirstOrDefault(item => item.WarehouseId == warehouseId);
        if (orderWarehouse is null) return NotFound();
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(item => item.Id == warehouseId, ct);
        if (warehouse is not null)
        {
            if (IsActiveInventoryStatus(order.OrderStatus)) warehouse.Amount += orderWarehouse.Amount;
            else if (IsReservedInventoryStatus(order.OrderStatus))
            {
                warehouse.ReservedAmount -= orderWarehouse.Amount;
                warehouse.Amount += orderWarehouse.Amount;
            }
        }
        _context.OrderWarehouses.Remove(orderWarehouse);
        await _context.SaveChangesAsync(ct);
        return Ok();
    }

    private string StatusSelectionKey() => $"orders:status-selection:{CurrentUserCacheId()}";
    private string DraftKey() => $"orders:create-draft:{CurrentUserCacheId()}";
    private string CurrentUserCacheId() => User.GetUserId() ??
        throw new UnauthorizedAccessException("Authenticated user ID is missing.");

    private static bool IsActiveInventoryStatus(int status) => status is
        OrderStatusCodes.New or OrderStatusCodes.Prepared or OrderStatusCodes.Processed or
        OrderStatusCodes.InDelivery or OrderStatusCodes.TemporarilyDelivered or OrderStatusCodes.Delivered or
        OrderStatusCodes.BalanceUpdated or OrderStatusCodes.Paid;

    private static bool IsReservedInventoryStatus(int status) =>
        status == OrderStatusCodes.Postponed || status is
            OrderStatusCodes.Incomplete or OrderStatusCodes.IncompleteStage1 or OrderStatusCodes.IncompleteStage2 or
            OrderStatusCodes.IncompleteStage3 or OrderStatusCodes.IncompleteStage4 or OrderStatusCodes.IncompleteStage5 or
            OrderStatusCodes.IncompleteStage6;
}

public record StatusSelectionRequest(List<int> OrderIds, int Status);
public record OrderDraftFieldRequest(string FieldName, string? Value);
public record FailedDeliveryRequest(string Reason);
public record UpdateAllStatusesRequest(List<int> OrderIds, int NewStatus, string? Reason);
public record UpdateInlineStoreRequest(int OrderId, int StoreId);
public record UpdateInlineFieldRequest(int OrderId, string FieldName, string? NewValue);
