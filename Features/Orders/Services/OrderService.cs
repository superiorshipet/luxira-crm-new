using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Repositories;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Services;

public partial class OrderService
{
    private static readonly ConcurrentDictionary<string, DateTime> CreationThrottle = new();
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromSeconds(30);

    private readonly OrderRepository _repository;
    private readonly ApplicationDbContext _context;
    private readonly OrderStatusTransitionPolicy _statusTransitionPolicy;
    private readonly IHubContext<OrderHub> _orderHub;
    private readonly S3StorageService _s3;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderRepository repository,
        ApplicationDbContext context,
        OrderStatusTransitionPolicy statusTransitionPolicy,
        IHubContext<OrderHub> orderHub,
        S3StorageService s3,
        ILogger<OrderService> logger)
    {
        _repository = repository;
        _context = context;
        _statusTransitionPolicy = statusTransitionPolicy;
        _orderHub = orderHub;
        _s3 = s3;
        _logger = logger;
    }

    public async Task<OrderListResult> GetOrdersAsync(OrderFilterRequest filter, CancellationToken ct = default)
    {
        var (items, totalCount) = await _repository.GetPagedOrdersAsync(filter, ct);
        int page = filter.Page > 0 ? filter.Page : 1;
        int pageSize = filter.PageSize > 0 && filter.PageSize <= 200 ? filter.PageSize : 50;
        int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var dtos = items.Select(MapToDto).ToList();
        return new OrderListResult(dtos, totalCount, page, pageSize, totalPages);
    }

    public async Task<OrderDto> GetOrderByIdAsync(int id, CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        return MapToDto(order);
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new BadRequestException("اسم العميل مطلوب.");

        if (string.IsNullOrWhiteSpace(request.TelephoneNumber))
            throw new BadRequestException("رقم هاتف العميل مطلوب.");

        if (request.DeliveryCompanyId <= 0)
            throw new BadRequestException("يجب اختيار شركة توصيل صالحة.");

        if (string.IsNullOrWhiteSpace(request.Address))
            throw new BadRequestException("عنوان العميل مطلوب.");

        var normalizedAddress = request.Address.Trim();

        if (request.OrderSource != 2 && string.IsNullOrWhiteSpace(request.ChatUrl))
            throw new BadRequestException("حقل رابط المحادثة مطلوب.");

        if (request.Country == 2 && ArabicRegex().IsMatch(request.Address ?? string.Empty))
            throw new BadRequestException("لا يُسمح بإدخال عنوان عربي لدولة تركيا. اكتب العنوان بحروف إنجليزية أو تركية فقط.");

        var normalizedPhone = NormalizePhoneNumber(request.TelephoneNumber, request.Country);
        var normalizedSecondPhone = !string.IsNullOrWhiteSpace(request.SecondTelephoneNumber)
            ? NormalizePhoneNumber(request.SecondTelephoneNumber, request.Country)
            : null;

        var throttleKey = $"{userId}_{normalizedPhone}";
        var now = IstanbulTimeHelper.Now;
        if (CreationThrottle.TryGetValue(throttleKey, out var lastSubmitTime) && (now - lastSubmitTime) < ThrottleWindow)
        {
            throw new BadRequestException("تم إرسال طلب لهذا الرقم قبل قليل، يرجى الانتظار 30 ثانية قبل إعادة المحاولة.");
        }
        var hasActiveOrder = await _context.Orders
            .AsNoTracking()
            .AnyAsync(o => o.TelephoneNumber == normalizedPhone
                        && o.ManufacturingCompanyId == request.ManufacturingCompanyId
                        && !OrderStatusCodes.ClosedStatuses.Contains(o.OrderStatus), ct);

        if (hasActiveOrder)
        {
            _logger.LogWarning("Active duplicate order detected for phone {Phone} and store {StoreId}", normalizedPhone, request.ManufacturingCompanyId);
        }

        int initialStatus = OrderStatusCodes.New;
        if (request.Country == 2 && (request.Address ?? string.Empty).Trim().Length < 15)
        {
            initialStatus = OrderStatusCodes.Incomplete;
        }

        if (request.Items != null && request.Items.Count > 0)
        {
            var minPrice = await _context.ProductMinimumSellingPrices
                .Where(p => p.Country == request.Country)
                .Select(p => p.MinimumPrice)
                .FirstOrDefaultAsync(ct);

            foreach (var item in request.Items)
            {
                if (minPrice > 0 && item.Price < minPrice)
                {
                    _logger.LogWarning("Order created below minimum price: {Price} < {MinPrice}", item.Price, minPrice);
                }
            }
        }

        var order = new Order
        {
            Country = request.Country,
            State = request.State,
            OrderSource = request.OrderSource,
            SourceName = request.SourceName,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            DeliveryCompanyId = request.DeliveryCompanyId,
            TelephoneNumber = normalizedPhone,
            SecondTelephoneNumber = normalizedSecondPhone,
            CustomerName = request.CustomerName.Trim(),
            Notes = request.Notes,
            Address = normalizedAddress,
            TotalPrice = request.TotalPrice,
            DeliveryPrice = request.DeliveryPrice,
            CustomerDeliveryPrice = request.CustomerDeliveryPrice,
            Chaturl = request.ChatUrl,
            ApplicationUserId = userId,
            OrderStatus = initialStatus,
            CreatedDate = now,
            LastEditedDate = now
        };

        if (request.Items != null)
        {
            foreach (var item in request.Items)
            {
                order.OrderWarehouses.Add(new OrderWarehouse
                {
                    WarehouseId = item.WarehouseId,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Cost = item.Cost
                });
            }
        }

        order.StatusHistories.Add(new OrderStatusHistory
        {
            Status = initialStatus,
            CreatedAt = now,
            ApplicationUserId = userId,
            Reason = "إنشاء الطلب في النظام",
            Name = $"PreviousStatus:{initialStatus}",
            IsHidden = false
        });
        order.EditHistories.Add(new OrderEditHistory
        {
            EditNumber = 1,
            Country = order.Country,
            State = order.State ?? string.Empty,
            OrderSource = order.OrderSource,
            SourceName = order.SourceName,
            ManufacturingCompanyId = order.ManufacturingCompanyId,
            DeliveryCompanyId = order.DeliveryCompanyId,
            TelephoneNumber = order.TelephoneNumber,
            SecondTelephoneNumber = order.SecondTelephoneNumber,
            CustomerName = order.CustomerName,
            Notes = order.Notes,
            Address = order.Address,
            CreatedDate = order.CreatedDate,
            LastEditedDate = now,
            OrderStatus = order.OrderStatus,
            TotalPrice = order.TotalPrice,
            ApplicationUserId = userId,
            DeliveryPrice = order.DeliveryPrice,
            Chaturl = order.Chaturl
        });

        await _context.Orders.AddAsync(order, ct);
        await _context.SaveChangesAsync(ct);
        CreationThrottle[throttleKey] = now;

        try
        {
            await _orderHub.Clients.Group("UsersExpectDelivery").SendAsync("OrderCreated", new
            {
                OrderId = order.Id,
                order.CustomerName,
                order.TelephoneNumber,
                order.TotalPrice,
                order.OrderStatus,
                StatusName = OrderStatusCodes.GetDisplayName(order.OrderStatus),
                CreatedAt = now
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast OrderCreated event for Order {OrderId}", order.Id);
        }

        return MapToDto(order);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(
        int id,
        UpdateOrderStatusRequest request,
        OrderStatusActor actor,
        CancellationToken ct = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order == null)
            throw new NotFoundException($"Order with ID {id} was not found.");

        var previousStatus = order.OrderStatus;
        _statusTransitionPolicy.EnsureAllowed(order, request.NewStatus, request.Reason, actor);

        if (previousStatus == request.NewStatus)
        {
            return MapToDto(order);
        }

        var now = IstanbulTimeHelper.Now;
        order.OrderStatus = request.NewStatus;
        order.LastEditedDate = now;
        order.Editedby = actor.UserId;

        if (request.NewStatus == OrderStatusCodes.Delivered && !order.IsBonusPaidForEmployee)
        {
            var bonusConfig = await _context.OrderBonusConfigurations
                .FirstOrDefaultAsync(
                    config => config.Country == order.Country && config.IsActive,
                    ct);
            if (bonusConfig is not null && bonusConfig.BonusAmount > 0)
            {
                order.IsBonus = true;
            }
        }

        var history = new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = request.NewStatus,
            CreatedAt = now,
            ApplicationUserId = actor.UserId,
            Reason = CombineHistoryReason(
                request.Reason ?? $"تغيير الحالة من {OrderStatusCodes.GetDisplayName(previousStatus)} إلى {OrderStatusCodes.GetDisplayName(request.NewStatus)}",
                request.Note),
            Name = $"PreviousStatus:{previousStatus}",
            IsHidden = false
        };

        _context.OrderStatusHistories.Add(history);
        await _context.SaveChangesAsync(ct);

        try
        {
            await _orderHub.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", new
            {
                OrderId = order.Id,
                PreviousStatus = previousStatus,
                NewStatus = order.OrderStatus,
                StatusName = OrderStatusCodes.GetDisplayName(order.OrderStatus),
                UpdatedBy = actor.UserId,
                UpdatedAt = now
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast OrderStatusUpdated for Order {OrderId}", order.Id);
        }

        return MapToDto(order);
    }

    private static string? CombineHistoryReason(string? reason, string? note)
    {
        if (string.IsNullOrWhiteSpace(reason)) return note?.Trim();
        if (string.IsNullOrWhiteSpace(note)) return reason.Trim();
        return $"{reason.Trim()} | {note.Trim()}";
    }

    public async Task<int> BatchUpdateOrderStatusAsync(
        BatchUpdateOrderStatusRequest request,
        OrderStatusActor actor,
        CancellationToken ct = default)
    {
        if (request.OrderIds == null || request.OrderIds.Count == 0)
            return 0;

        int updated = 0;
        foreach (var id in request.OrderIds)
        {
            try
            {
                await UpdateOrderStatusAsync(id, new UpdateOrderStatusRequest(request.NewStatus, request.Reason, request.Note), actor, ct);
                updated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed batch update status for Order {OrderId}", id);
            }
        }
        return updated;
    }

    public async Task<OrderDto> UpdateInlineFieldAsync(int orderId, string fieldName, string? newValue, string userId, CancellationToken ct = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null)
            throw new NotFoundException($"Order with ID {orderId} was not found.");

        var nextEditNumber = (await _context.OrderEditHistories
            .Where(history => history.OrderId == order.Id)
            .MaxAsync(history => (int?)history.EditNumber, ct) ?? 0) + 1;
        var editHistory = CreateEditSnapshot(order, nextEditNumber, userId);

        var now = IstanbulTimeHelper.Now;
        switch (fieldName.ToLowerInvariant())
        {
            case "notes": order.Notes = newValue; break;
            case "address": order.Address = newValue ?? string.Empty; break;
            case "customername": order.CustomerName = newValue ?? string.Empty; break;
            case "telephonenumber": order.TelephoneNumber = newValue ?? string.Empty; break;
            case "secondtelephonenumber": order.SecondTelephoneNumber = newValue; break;
            case "state": order.State = newValue; break;
            case "deliverycompanyid":
                if (!int.TryParse(newValue, out var deliveryCompanyId) || deliveryCompanyId <= 0)
                    throw new BadRequestException("Delivery company ID must be a positive integer.");
                order.DeliveryCompanyId = deliveryCompanyId;
                break;
            case "totalprice":
                if (!decimal.TryParse(newValue, out var totalPrice) || totalPrice < 0)
                    throw new BadRequestException("Total price must be a non-negative number.");
                order.TotalPrice = totalPrice;
                break;
            case "delegateemployeeid": order.DelegateEmployeeId = newValue; break;
            default:
                throw new BadRequestException($"Unsupported field name '{fieldName}' for inline update.");
        }

        order.LastEditedDate = now;
        order.Editedby = userId;

        await _context.OrderEditHistories.AddAsync(editHistory, ct);
        await _context.SaveChangesAsync(ct);
        return MapToDto(order);
    }

    public async Task<OrderDto> MarkAsBankTransferAsync(
        int orderId,
        string userId,
        CancellationToken ct = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null)
            throw new NotFoundException($"Order with ID {orderId} was not found.");

        if (order.IsPaid)
            return MapToDto(order);

        var nextEditNumber = (await _context.OrderEditHistories
            .Where(history => history.OrderId == order.Id)
            .MaxAsync(history => (int?)history.EditNumber, ct) ?? 0) + 1;
        await _context.OrderEditHistories.AddAsync(
            CreateEditSnapshot(order, nextEditNumber, userId),
            ct);

        order.IsPaid = true;
        order.LastEditedDate = IstanbulTimeHelper.Now;
        order.Editedby = userId;

        await _context.SaveChangesAsync(ct);
        return MapToDto(order);
    }

    private static OrderEditHistory CreateEditSnapshot(
        Order order,
        int editNumber,
        string userId) =>
        new()
        {
            OrderId = order.Id,
            EditNumber = editNumber,
            Country = order.Country,
            State = order.State ?? string.Empty,
            OrderSource = order.OrderSource,
            SourceName = order.SourceName,
            ManufacturingCompanyId = order.ManufacturingCompanyId,
            DeliveryCompanyId = order.DeliveryCompanyId,
            TelephoneNumber = order.TelephoneNumber,
            SecondTelephoneNumber = order.SecondTelephoneNumber,
            CustomerName = order.CustomerName,
            Notes = order.Notes,
            Address = order.Address,
            CreatedDate = order.CreatedDate,
            LastEditedDate = order.LastEditedDate,
            FixedOrderDate = order.FixedOrderDate,
            InstantAddedDate = order.InstantAddedDate,
            OrderStatus = order.OrderStatus,
            TotalPrice = order.TotalPrice,
            ExternalOrderId = order.ExternalOrderId,
            ApplicationUserId = order.ApplicationUserId ?? userId,
            ExternalShipmentCode = order.CamexTrackingNumber?.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            FromComments = order.FromComments,
            Gender = order.Gender,
            IsPaid = order.IsPaid,
            Editedby = userId,
            FromOffers = false,
            CampaignId = order.CampaignId,
            DeliveryPrice = order.DeliveryPrice,
            Chaturl = order.Chaturl,
        };

    public async Task<List<OrderDto>> CheckDuplicatesAsync(string phoneNumber, CancellationToken ct = default)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .AsNoTracking()
            .Where(o => o.TelephoneNumber == phoneNumber || o.SecondTelephoneNumber == phoneNumber)
            .OrderByDescending(o => o.CreatedDate)
            .Take(10)
            .ToListAsync(ct);

        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderStatsDto> GetStatsAsync(int? country = null, CancellationToken ct = default)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();
        if (country.HasValue) query = query.Where(o => o.Country == country.Value);

        var total = await query.CountAsync(ct);
        var newOrders = await query.CountAsync(o => o.OrderStatus == OrderStatusCodes.New, ct);
        var delivered = await query.CountAsync(o => o.OrderStatus == OrderStatusCodes.Delivered, ct);
        var returned = await query.CountAsync(o => o.OrderStatus == OrderStatusCodes.Returned, ct);
        var cancelled = await query.CountAsync(o => o.OrderStatus == OrderStatusCodes.Cancelled, ct);
        var totalRev = await query.Where(o => o.OrderStatus == OrderStatusCodes.Delivered).SumAsync(o => (decimal?)o.TotalPrice, ct) ?? 0;

        return new OrderStatsDto(total, newOrders, delivered, returned, cancelled, totalRev);
    }

    private static string NormalizePhoneNumber(string phone, int country)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (country == 1) // العراق
        {
            if (digits.StartsWith("00964", StringComparison.Ordinal)) digits = digits[5..];
            else if (digits.StartsWith("964", StringComparison.Ordinal)) digits = digits[3..];
            else if (digits.StartsWith('0')) digits = digits[1..];
            return $"964{digits}";
        }
        else if (country == 2) // تركيا
        {
            if (digits.StartsWith("0090", StringComparison.Ordinal)) digits = digits[4..];
            else if (digits.StartsWith("90", StringComparison.Ordinal)) digits = digits[2..];
            else if (digits.StartsWith('0')) digits = digits[1..];
            return $"90{digits}";
        }

        return digits.TrimStart('0');
    }

    private static OrderDto MapToDto(Order o) => new(
        o.Id,
        o.Country,
        o.State,
        o.OrderSource,
        o.SourceName,
        o.ManufacturingCompanyId,
        o.DeliveryCompanyId,
        o.DeliveryCompany?.Name,
        o.TelephoneNumber,
        o.SecondTelephoneNumber,
        o.CustomerName,
        o.Notes,
        o.Address,
        o.CreatedDate,
        o.LastEditedDate,
        o.OrderStatus,
        OrderStatusCodes.GetDisplayName(o.OrderStatus),
        o.TotalPrice,
        o.DeliveryPrice,
        o.CustomerDeliveryPrice,
        o.ApplicationUserId,
        o.ApplicationUser?.UserName,
        o.IsPinned,
        o.IsPaid,
        o.IsDelayed,
        o.OrderWarehouses.Select(w => new OrderItemDto(w.Id, w.WarehouseId, w.Quantity, w.Price, w.Cost)).ToList()
    );

    [GeneratedRegex(@"[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]")]
    private static partial Regex ArabicRegex();
}
