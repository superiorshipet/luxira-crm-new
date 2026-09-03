using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Infrastructure.Caching;
using Luxira.Api.Infrastructure.S3;
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
    private readonly S3StorageService _storage;

    public OrderController(
        OrderService orderService,
        ApplicationDbContext context,
        LuxiraCacheService cache,
        IHubContext<OrderHub> hub,
        S3StorageService storage)
    {
        _orderService = orderService;
        _context = context;
        _cache = cache;
        _hub = hub;
        _storage = storage;
    }

    [HttpGet]
    [HttpGet("/Order/GetOrders")]
    [HttpGet("/Order/Index")]
    [HttpPost("/Order/Index")]
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
        if (User.IsInRole("CallCenter"))
        {
            var activityUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            HttpContext.Session.SetString(HomeController.ActivityKey(userId, "LastOrderCreatedUnix"), activityUnix);
            HttpContext.Session.SetString(HomeController.ActivityKey(userId, "LastCreateOrderOpenedUnix"), activityUnix);
        }
        return CreatedAtAction(nameof(GetOrderById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HttpPost("Edit/{id:int}")]
    [HttpPost("/Order/Edit/{id:int}")]
    public async Task<IActionResult> EditOrder([RouteOrRequest] int id, [FromBody] CreateOrderRequest request, CancellationToken ct)
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
        [RouteOrRequest] int id,
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

    [HttpGet("status-history/{orderId:int}")]
    [HttpGet("/Order/GetOrderStatusHistory")]
    public async Task<IActionResult> GetOrderStatusHistory([FromQuery] int? id, int? orderId, CancellationToken ct)
    {
        var targetId = orderId ?? id ?? 0;
        if (targetId <= 0) return BadRequest(new { success = false, message = "رقم الطلب غير صحيح." });
        var items = await (from history in _context.OrderStatusHistories.AsNoTracking()
                           join snapshot in _context.OrderStatusHistoryDeliveryCompanySnapshots.AsNoTracking()
                               on history.Id equals snapshot.OrderStatusHistoryId into snapshots
                           from snapshot in snapshots.DefaultIfEmpty()
                           where history.OrderId == targetId
                           orderby history.CreatedAt descending, history.Id descending
                           select new
                           {
                               history.Id, history.OrderId, history.Status, history.Reason, history.Name, history.CreatedAt,
                               history.ApplicationUserId, history.FailureReasonImageUrl, history.IsHidden,
                               deliveryCompanyId = snapshot == null ? null : snapshot.DeliveryCompanyId,
                               deliveryCompanyName = snapshot == null ? string.Empty : snapshot.DeliveryCompanyName ?? string.Empty
                           }).ToListAsync(ct);
        return Ok(new { success = true, orderId = targetId, items });
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
    public async Task<IActionResult> GetOrderPricingSelection([RouteOrRequest] int id, [FromQuery] int? orderId, CancellationToken ct)
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
    public async Task<IActionResult> ClearStatusUpdateSelection([RouteOrRequest] int orderId, CancellationToken ct)
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
    public async Task<IActionResult> MoveOrderToYesterdayRatings([RouteOrRequest] int id, [FromQuery] int? orderId, CancellationToken ct)
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
    public async Task<IActionResult> BlockDuplicateOrder([RouteOrRequest] int id, [FromQuery] int? orderId, CancellationToken ct)
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
    public async Task<IActionResult> UpdateDelivered([RouteOrRequest] int id, [FromQuery] int? orderId, CancellationToken ct)
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
    public async Task<IActionResult> UpdateFailedDelivery([RouteOrRequest] int id, [FromQuery] int? orderId, [FromBody] FailedDeliveryRequest request, CancellationToken ct)
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
    public async Task<IActionResult> GetOrderInlineEditOptions([RouteOrRequest] int orderId, [FromQuery] string field, CancellationToken ct)
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

    [HttpGet("/Order/UpdateAllStatuses")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,OrderPreparer")]
    public async Task<IActionResult> UpdateAllStatusesPage([FromQuery] OrderFilterRequest filter, CancellationToken ct) =>
        Ok(await _orderService.GetOrdersAsync(filter with { PageSize = Math.Clamp(filter.PageSize, 1, 200) }, ct));

    [HttpGet("/Order/CanEnterFailedDeliveryStatusUpdate")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> CanEnterFailedDeliveryStatusUpdate(CancellationToken ct)
    {
        if (!User.IsInRole("FollowUpDepartment"))
            return Ok(new { success = true, allowed = true, unresolvedCount = 0 });

        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Ok(new { success = true, allowed = false, unresolvedCount = 0, message = "لا يمكنك تحديث حالات فشل أخرى قبل حل الحالات الحالية." });

        var accessibleStores = _context.EmployeeManufacturingCompanies.AsNoTracking()
            .Where(access => access.ApplicationUserId == userId && access.CanSeeManufacturingCompany)
            .Select(access => access.ManufacturingCompanyId);
        var failures = OrderStatusCodes.FailureStatuses;
        var unresolvedCount = await _context.Orders.AsNoTracking().CountAsync(order =>
            !order.IsHidden &&
            order.ManufacturingCompanyId.HasValue &&
            accessibleStores.Contains(order.ManufacturingCompanyId.Value) &&
            failures.Contains(order.OrderStatus), ct);

        return Ok(new
        {
            success = true,
            allowed = unresolvedCount == 0,
            unresolvedCount,
            message = unresolvedCount == 0 ? "" : "لا يمكنك تحديث حالات فشل أخرى وأنت لديك بالفعل حالات فشل تسليم. تواصل مع العملاء لحلها، ثم حدّث مرة أخرى."
        });
    }

    [HttpGet("/Order/UpdateFailedDelivery")]
    [HttpGet("/Order/UpdateDelivered")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> DeliveryStatusUpdatePage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? countryId = null,
        [FromQuery] int? storeId = null,
        [FromQuery] int? deliverycompanyId = null,
        [FromQuery] int? deliveryrepresentativeId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _context.Orders.AsNoTracking().Where(order => !order.IsHidden && order.OrderStatus == OrderStatusCodes.InDelivery);
        if (countryId.HasValue) query = query.Where(order => order.Country == countryId);
        if (storeId is > 0) query = query.Where(order => order.ManufacturingCompanyId == storeId);
        if (deliverycompanyId is > 0) query = query.Where(order => order.DeliveryCompanyId == deliverycompanyId);
        if (deliveryrepresentativeId is > 0) query = query.Where(order => order.DeliveryCompanyId == deliveryrepresentativeId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = int.TryParse(value.TrimStart('#'), out var orderId)
                ? query.Where(order => order.Id == orderId || order.ExternalOrderId == orderId)
                : query.Where(order => order.CustomerName == value || order.TelephoneNumber == value || order.SecondTelephoneNumber == value);
        }
        var totalItems = await query.CountAsync(ct);
        var items = await query.OrderByDescending(order => order.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(order => new { order.Id, order.ExternalOrderId, order.CustomerName, order.TelephoneNumber, order.Country, order.State, order.TotalPrice, order.DeliveryCompanyId, order.ManufacturingCompanyId, order.CreatedDate })
            .ToListAsync(ct);
        return Ok(new { items, currentPage = page, pageSize, totalItems });
    }

    [HttpGet("/Order/UpdateDeliverySplit")]
    [HttpPost("/Order/UpdateDeliverySplit")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public IActionResult UpdateDeliverySplit() => Ok(new
    {
        failedDeliveryUrl = "/Order/UpdateFailedDelivery",
        deliveredUrl = "/Order/UpdateDelivered"
    });

    [HttpPost("/Order/HiddenOrders")]
    public Task<IActionResult> HiddenOrdersPost([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        HiddenOrders(page, pageSize, ct);

    [HttpPost("/Order/HasCreateOrderDraftImage")]
    public async Task<IActionResult> HasCreateOrderDraftImage([FromForm] string draftId, [FromForm] string imageType, CancellationToken ct)
    {
        var type = NormalizeDraftImageType(imageType);
        if (string.IsNullOrWhiteSpace(draftId) || type is null) return Ok(new { exists = false });
        var images = await _cache.GetOrCreateAsync(DraftImagesKey(draftId), _ => Task.FromResult(new Dictionary<string, string>()), TimeSpan.FromDays(1), ct: ct);
        return Ok(new { exists = images.TryGetValue(type, out var url) && !string.IsNullOrWhiteSpace(url) });
    }

    [HttpPost("/Order/UploadCreateOrderDraftImage")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadCreateOrderDraftImage(
        [FromForm] string draftId,
        [FromForm] string imageType,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        var type = NormalizeDraftImageType(imageType);
        if (string.IsNullOrWhiteSpace(draftId) || type is null || file is null || file.Length == 0 || !(file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
            return BadRequest(new { success = false, message = "الصورة غير صالحة." });

        var stored = await _storage.UploadAsync(file, type == "order" ? "images/orders" : "images/receipts", User.GetUserId(), ct);
        var images = await _cache.GetOrCreateAsync(DraftImagesKey(draftId), _ => Task.FromResult(new Dictionary<string, string>()), TimeSpan.FromDays(1), ct: ct);
        var imageUrl = stored.PublicUrl ?? throw new InvalidOperationException("Uploaded image URL is missing.");
        images[type] = imageUrl;
        await _cache.SetAsync(DraftImagesKey(draftId), images, TimeSpan.FromDays(1), ct: ct);
        return Ok(new { success = true, imageUrl });
    }

    [AllowAnonymous]
    [HttpGet("/t/{trackingCode}")]
    [HttpGet("/Order/TrackLavaShipment/{trackingCode}")]
    [HttpGet("/Order/TrackFlareShipment/{trackingCode}")]
    [HttpGet("/Order/TrackLoxxKingShipment/{trackingCode}")]
    [HttpGet("/Order/TrackHayatShipment/{trackingCode}")]
    [HttpGet("/Order/TrackLioraShipment/{trackingCode}")]
    [HttpGet("/Order/TrackShipment/{trackingCode}")]
    public async Task<IActionResult> TrackShipment(string trackingCode, CancellationToken ct)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive, nosnippet";
        if (!TryResolveTrackingCode(trackingCode, out var orderId)) return NotFound();
        var order = await _context.Orders.AsNoTracking()
            .Where(item => item.Id == orderId && !item.IsHidden)
            .Select(item => new
            {
                item.Id,
                item.CustomerName,
                item.OrderStatus,
                statusText = OrderStatusCodes.GetDisplayName(item.OrderStatus),
                item.CreatedDate,
                item.FixedOrderDate,
                storeName = _context.ManufacturingCompanies.Where(store => store.Id == item.ManufacturingCompanyId).Select(store => store.Name).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("/Order/GetOrderFollowUpStatus")]
    public async Task<IActionResult> GetOrderFollowUpStatus([FromQuery] int orderId, [FromQuery] string requestType = "Complaint", CancellationToken ct = default)
    {
        var type = NormalizeFollowUpType(requestType);
        if (orderId <= 0 || type is null) return BadRequest(new { success = false, message = "الطلب أو نوع المتابعة غير صحيح." });
        var request = await _context.OrderFollowUpRequests.AsNoTracking()
            .Where(item => item.OrderId == orderId && item.RequestType == type)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var state = request is null ? "none" : request.IsClosed ? "closed" : request.ProcessingStartedAt.HasValue ? "processing" : "new";
        return Ok(new
        {
            success = true,
            state,
            buttonText = state switch { "processing" => "قيد المعالجة", "new" => "تم الإرسال", _ => "إرسال" },
            responsibleName = request?.ProcessingStartedByName ?? request?.ClosedByName ?? "",
            processingStartedAt = request?.ProcessingStartedAt,
            closedAt = request?.ClosedAt
        });
    }

    [HttpPost("/Order/CreateOrderFollowUpRequest")]
    public async Task<IActionResult> CreateOrderFollowUpRequest([FromBody] CreateFollowUpRequest request, CancellationToken ct)
    {
        var type = NormalizeFollowUpType(request.RequestType);
        var note = request.Note?.Trim();
        if (request.OrderId <= 0 || type is null || note?.Length > 2000 || (string.IsNullOrWhiteSpace(note) && string.IsNullOrWhiteSpace(request.ImageData)))
            return BadRequest(new { success = false, message = "بيانات المتابعة غير صحيحة." });
        if (!await _context.Orders.AsNoTracking().AnyAsync(order => order.Id == request.OrderId, ct)) return NotFound(new { success = false, message = "الطلب غير موجود." });
        if (type == "Complaint" && await _context.OrderFollowUpRequests.AsNoTracking().AnyAsync(item => item.OrderId == request.OrderId && item.RequestType == type && !item.IsClosed, ct))
            return Conflict(new { success = false, message = "تم إرسال الشكوى بالفعل." });

        string? imageUrl = null;
        string? imageKey = null;
        if (!string.IsNullOrWhiteSpace(request.ImageData))
        {
            var parsed = ParseImageData(request.ImageData);
            await using var stream = new MemoryStream(parsed.Bytes, writable: false);
            var stored = await _storage.UploadStreamAsync(stream, parsed.Bytes.Length, "order-follow-up", $"follow-up-{request.OrderId}.{parsed.Extension}", parsed.ContentType, User.GetUserId(), ct);
            imageUrl = stored.PublicUrl;
            imageKey = stored.S3Key;
        }

        var entity = new OrderFollowUpRequest
        {
            OrderId = request.OrderId,
            RequestType = type,
            Note = note,
            ImagePath = imageUrl,
            ImageS3Key = imageKey,
            CreatedByUserId = User.GetUserId(),
            CreatedByName = User.Identity?.Name,
            CreatedAt = IstanbulTimeHelper.Now
        };
        _context.OrderFollowUpRequests.Add(entity);
        await _context.SaveChangesAsync(ct);
        await _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId = request.OrderId, reason = "follow_up_created" }, ct);
        return Ok(new { success = true, id = entity.Id, state = "new" });
    }

    [HttpGet("/Order/GetLatestFailureReasonImage")]
    public async Task<IActionResult> GetLatestFailureReasonImage([FromQuery] int orderId, CancellationToken ct)
    {
        if (orderId <= 0) return BadRequest(new { success = false, message = "رقم الطلب غير صحيح" });
        var canSee = await _context.Orders.AsNoTracking().AnyAsync(order => order.Id == orderId && !order.IsHidden, ct);
        if (!canSee) return NotFound(new { success = false, message = "الطلب غير موجود أو ليس لديك صلاحية عليه" });
        var imageUrl = await _context.OrderStatusHistories.AsNoTracking()
            .Where(history => history.OrderId == orderId && history.FailureReasonImageUrl != null && history.FailureReasonImageUrl != "")
            .OrderByDescending(history => history.CreatedAt)
            .Select(history => history.FailureReasonImageUrl)
            .FirstOrDefaultAsync(ct);
        return Ok(new { success = true, orderId, imageUrl = imageUrl ?? "" });
    }

    [HttpPost("/Order/UpdateStatus")]
    public async Task<IActionResult> UpdateStatusLegacy(
        [FromForm] int id,
        [FromForm] int orderStatus,
        [FromForm] string? reason,
        CancellationToken ct)
    {
        if (!OrderStatusCodes.IsDefined(orderStatus)) return BadRequest(new { success = false, message = "حالة الطلب غير صحيحة." });
        var result = await _orderService.UpdateOrderStatusAsync(
            id,
            new UpdateOrderStatusRequest(orderStatus, reason, null),
            OrderStatusActor.FromPrincipal(User),
            ct);
        return Ok(new { success = true, order = result });
    }

    [HttpPost("/Order/UpdateOrderApplicationUser")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> UpdateOrderApplicationUser(
        [FromForm] int orderId,
        [FromForm] string newApplicationUserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(newApplicationUserId)) return BadRequest("Invalid user ID provided.");
        if (!await _context.Employees.AsNoTracking().AnyAsync(employee => employee.ApplicationUserId == newApplicationUserId, ct))
            return NotFound($"Employee with ID {newApplicationUserId} not found.");
        var changed = await _context.Orders.Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.ApplicationUserId, newApplicationUserId)
                .SetProperty(order => order.LastEditedDate, IstanbulTimeHelper.Now), ct);
        if (changed == 0) return NotFound($"Order with ID {orderId} not found.");
        await _hub.Clients.All.SendAsync("OrderRealtimeChanged", new { orderId, reason = "assigned_employee_updated" }, ct);
        return Ok(new { success = true, orderId, applicationUserId = newApplicationUserId });
    }

    [HttpPost("/Order/UpdateStatusForMultiple")]
    public async Task<IActionResult> UpdateStatusForMultiple(
        [FromForm] List<int>? ids,
        [FromForm] int orderStatus,
        [FromForm] string? reason,
        CancellationToken ct)
    {
        var orderIds = (ids ?? []).Where(id => id > 0).Distinct().Take(5_000).ToList();
        if (orderIds.Count == 0 || !OrderStatusCodes.IsDefined(orderStatus)) return BadRequest(new { success = false, message = "لم يتم تحديد طلبات أو حالة صحيحة." });
        var count = await _orderService.BatchUpdateOrderStatusAsync(
            new BatchUpdateOrderStatusRequest(orderIds, orderStatus, reason, null),
            OrderStatusActor.FromPrincipal(User),
            ct);
        return Ok(new { success = true, updatedCount = count });
    }

    [HttpPost("/Order/AdvanceFailureStatus")]
    public async Task<IActionResult> AdvanceFailureStatus([FromForm] List<int>? ids, [FromForm] string? reason, CancellationToken ct)
    {
        var orderIds = (ids ?? []).Where(id => id > 0).Distinct().Take(5_000).ToList();
        var statuses = await _context.Orders.AsNoTracking().Where(order => orderIds.Contains(order.Id))
            .Select(order => new { order.Id, order.OrderStatus }).ToListAsync(ct);
        var updated = 0;
        foreach (var group in statuses.GroupBy(item => NextFailureStatus(item.OrderStatus)).Where(group => group.Key.HasValue))
        {
            updated += await _orderService.BatchUpdateOrderStatusAsync(
                new BatchUpdateOrderStatusRequest(group.Select(item => item.Id).ToList(), group.Key!.Value, reason, null),
                OrderStatusActor.FromPrincipal(User),
                ct);
        }
        return Ok(new { success = true, updatedCount = updated });
    }

    [HttpPost("/Order/MarkAsPrepared")]
    public async Task<IActionResult> MarkAsPrepared([FromForm] List<int>? ids, CancellationToken ct)
    {
        var orderIds = (ids ?? []).Where(id => id > 0).Distinct().Take(5_000).ToList();
        if (orderIds.Count == 0) return BadRequest(new { success = false, message = "No orders found to update." });
        var updated = await _orderService.BatchUpdateOrderStatusAsync(
            new BatchUpdateOrderStatusRequest(orderIds, OrderStatusCodes.Prepared, null, null),
            OrderStatusActor.FromPrincipal(User),
            ct);
        return Ok(new { success = true, updatedCount = updated });
    }

    [HttpPost("/Order/DeleteOrderStatusHistoryAsync")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteOrderStatusHistoryAsync([FromForm] int id, CancellationToken ct)
    {
        var history = await _context.OrderStatusHistories.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (history is null) return NotFound(new { success = false, message = "Order status history not found" });
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var latest = await _context.OrderStatusHistories.Where(item => item.OrderId == history.OrderId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(2).ToListAsync(ct);
        if (latest.FirstOrDefault()?.Id == history.Id && latest.ElementAtOrDefault(1)?.Status is int previousStatus && history.OrderId.HasValue)
            await _context.Orders.Where(order => order.Id == history.OrderId.Value)
                .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.OrderStatus, previousStatus), ct);
        _context.OrderStatusHistories.Remove(history);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await _hub.Clients.All.SendAsync("OrderStatusHistoryDelete", new { orderId = history.OrderId, historyId = id, isDeleted = true, isHidden = false }, ct);
        return Ok(new { success = true });
    }

    [HttpPost("/Order/DeleteOrdersStatusHistoryAsync")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteOrdersStatusHistoryAsync([FromForm] string ids, CancellationToken ct)
    {
        var orderIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).Distinct().Take(5_000).ToList();
        if (orderIds.Count == 0) return BadRequest(new { success = false, message = "Invalid Order IDs format" });
        var histories = await _context.OrderStatusHistories.Where(item => item.OrderId.HasValue && orderIds.Contains(item.OrderId.Value))
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).ToListAsync(ct);
        var latestByOrder = histories.GroupBy(item => item.OrderId!.Value).Select(group => group.First()).ToList();
        var priorStatuses = histories.GroupBy(item => item.OrderId!.Value)
            .Select(group => new { OrderId = group.Key, Status = group.Skip(1).Select(item => item.Status).FirstOrDefault() })
            .Where(item => item.Status.HasValue).ToList();
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        foreach (var prior in priorStatuses)
            await _context.Orders.Where(order => order.Id == prior.OrderId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.OrderStatus, prior.Status!.Value), ct);
        _context.OrderStatusHistories.RemoveRange(latestByOrder);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        var deleted = latestByOrder.Select(item => new { orderId = item.OrderId, historyId = item.Id, isDeleted = true, isHidden = false }).ToList();
        await _hub.Clients.All.SendAsync("OrderStatusHistoryDelete", deleted, ct);
        return Ok(new { success = true, deletedCount = deleted.Count, deletedIds = latestByOrder.Select(item => item.Id) });
    }

    private string StatusSelectionKey() => $"orders:status-selection:{CurrentUserCacheId()}";
    private string DraftKey() => $"orders:create-draft:{CurrentUserCacheId()}";
    private string DraftImagesKey(string draftId) => $"orders:create-draft-images:{CurrentUserCacheId()}:{draftId.Trim()}";
    private static string? NormalizeDraftImageType(string? value) => value?.Trim().ToLowerInvariant() is "order" or "receipt" ? value.Trim().ToLowerInvariant() : null;
    private static string? NormalizeFollowUpType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "complaint" or "complaints" or "شكوى" or "شكوي" or "شكاوى" or "شكاوي" => "Complaint",
        "productinterest" or "productlike" or "potentialcustomer" or "إعجاب بالمنتج" or "الاعجاب بالمنتج" or "عميل محتمل" => "ProductInterest",
        _ => null
    };

    private static (byte[] Bytes, string ContentType, string Extension) ParseImageData(string imageData)
    {
        var comma = imageData.IndexOf(',');
        var metadata = comma >= 0 ? imageData[..comma] : "data:image/jpeg;base64";
        var payload = comma >= 0 ? imageData[(comma + 1)..] : imageData;
        var contentType = metadata.Contains("image/png", StringComparison.OrdinalIgnoreCase) ? "image/png" :
            metadata.Contains("image/webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" : "image/jpeg";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(payload); }
        catch (FormatException) { throw new BadRequestException("الصورة غير صالحة."); }
        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024) throw new BadRequestException("حجم الصورة غير صالح.");
        return (bytes, contentType, contentType[(contentType.IndexOf('/') + 1)..]);
    }

    private static bool TryResolveTrackingCode(string? value, out int orderId)
    {
        orderId = 0;
        var code = value?.Trim() ?? "";
        if (int.TryParse(code, out orderId) && orderId > 0) return true;
        if (code.StartsWith("trk-", StringComparison.OrdinalIgnoreCase)) code = code[4..];
        return TryDecodeTrackingCode(code, "0123456789abcdefghijklmnopqrstuvwyz", out orderId) ||
               TryDecodeTrackingCode(code, "0123456789abcdefghijklmnopqrstuvwxyz", out orderId);
    }

    private static bool TryDecodeTrackingCode(string code, string alphabet, out int orderId)
    {
        orderId = 0;
        if (string.IsNullOrWhiteSpace(code)) return false;
        long value = 0;
        foreach (var character in code.ToLowerInvariant())
        {
            var digit = alphabet.IndexOf(character);
            if (digit < 0) return false;
            if (value > (long.MaxValue - digit) / alphabet.Length) return false;
            value = value * alphabet.Length + digit;
        }
        var raw = value - 173891;
        if (raw <= 0 || raw % 7919 != 0 || raw / 7919 > int.MaxValue) return false;
        orderId = (int)(raw / 7919);
        return true;
    }
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

    private static int? NextFailureStatus(int status) => status switch
    {
        OrderStatusCodes.FailedDelivery => OrderStatusCodes.FailedDeliveryStage2,
        OrderStatusCodes.FailedDeliveryStage1 => OrderStatusCodes.FailedDeliveryStage2,
        OrderStatusCodes.FailedDeliveryStage2 => OrderStatusCodes.FailedDeliveryStage3,
        OrderStatusCodes.FailedDeliveryStage3 => OrderStatusCodes.FailedDeliveryStage4,
        OrderStatusCodes.FailedDeliveryStage4 => OrderStatusCodes.FailedDeliveryStage5,
        OrderStatusCodes.FailedDeliveryStage5 => OrderStatusCodes.FailedDeliveryStage6,
        OrderStatusCodes.FailedDeliveryStage6 => OrderStatusCodes.FailedDeliveryStage7,
        _ => null
    };
}

public record StatusSelectionRequest(List<int> OrderIds, int Status);
public record OrderDraftFieldRequest(string FieldName, string? Value);
public record FailedDeliveryRequest(string Reason);
public record UpdateAllStatusesRequest(List<int> OrderIds, int NewStatus, string? Reason);
public record UpdateInlineStoreRequest(int OrderId, int StoreId);
public record UpdateInlineFieldRequest(int OrderId, string FieldName, string? NewValue);
public record CreateFollowUpRequest(int OrderId, string? RequestType, string? Note, string? ImageData);
