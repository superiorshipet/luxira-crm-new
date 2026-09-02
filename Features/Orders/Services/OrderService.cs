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
    private const int TurkeyCountryId = 7;
    private static readonly IReadOnlyDictionary<int, string> CountryDialCodes = new Dictionary<int, string>
    {
        [1] = "964", [2] = "971", [3] = "974", [4] = "218",
        [5] = "968", [6] = "970", [7] = "90", [8] = "962",
        [9] = "965", [10] = "973", [11] = "966", [12] = "216",
        [13] = "212", [14] = "213", [15] = "961", [16] = "20",
    };
    private static readonly HashSet<int> NoLeadingZeroLocalCountries = [3, 5, 9, 10, 12];
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

        if (request.Country == TurkeyCountryId && ArabicRegex().IsMatch(request.Address ?? string.Empty))
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
        if (request.Country == TurkeyCountryId && (request.Address ?? string.Empty).Trim().Length < 15)
        {
            initialStatus = OrderStatusCodes.Incomplete;
        }

        if (request.Items != null && request.Items.Count > 0)
        {
            var minPrice = await _context.ProductMinimumSellingPrices
                .Where(p => p.Country == request.Country)
                .Select(p => p.MinimumSellingPrice)
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
                    Amount = item.Quantity,
                    UnitPrice = item.Price
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
                    config => config.Country == order.Country &&
                        (config.EmployeeId == null || _context.Employees.Any(employee =>
                            employee.Id == config.EmployeeId &&
                            employee.ApplicationUserId == order.ApplicationUserId)),
                    ct);
            if (bonusConfig is not null &&
                ((bonusConfig.FlatBonusAmount > 0 && order.TotalPrice >= bonusConfig.OrderThreshold) ||
                 bonusConfig.PercentageBonus > 0))
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

        var ids = request.OrderIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return 0;
        if (ids.Count > 500)
            throw new BadRequestException("A maximum of 500 orders can be updated per request.");

        var orders = await _context.Orders
            .Include(order => order.DeliveryCompany)
            .Where(order => ids.Contains(order.Id))
            .ToListAsync(ct);
        if (orders.Count != ids.Count)
        {
            var found = orders.Select(order => order.Id).ToHashSet();
            var missing = ids.Where(id => !found.Contains(id));
            throw new NotFoundException($"Orders were not found: {string.Join(',', missing)}.");
        }

        foreach (var order in orders)
            _statusTransitionPolicy.EnsureAllowed(order, request.NewStatus, request.Reason, actor);

        HashSet<int> bonusCountries = [];
        if (request.NewStatus == OrderStatusCodes.Delivered)
        {
            var countries = orders.Select(order => order.Country).Distinct().ToList();
            bonusCountries = (await _context.OrderBonusConfigurations.AsNoTracking()
                .Where(config => countries.Contains(config.Country) &&
                    (config.FlatBonusAmount > 0 || config.PercentageBonus > 0))
                .Select(config => config.Country)
                .ToListAsync(ct)).ToHashSet();
        }

        var now = IstanbulTimeHelper.Now;
        var changed = new List<(Order Order, int PreviousStatus)>();
        foreach (var order in orders)
        {
            var previousStatus = order.OrderStatus;
            if (previousStatus == request.NewStatus) continue;

            order.OrderStatus = request.NewStatus;
            order.LastEditedDate = now;
            order.Editedby = actor.UserId;
            if (request.NewStatus == OrderStatusCodes.Delivered &&
                !order.IsBonusPaidForEmployee && bonusCountries.Contains(order.Country))
                order.IsBonus = true;

            _context.OrderStatusHistories.Add(new OrderStatusHistory
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
            });
            changed.Add((order, previousStatus));
        }

        if (changed.Count == 0) return 0;
        await _context.SaveChangesAsync(ct);

        foreach (var (order, previousStatus) in changed)
        {
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
                _logger.LogError(ex, "Failed to broadcast batch OrderStatusUpdated for Order {OrderId}", order.Id);
            }
        }

        return changed.Count;
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
        var buffer = new char[phone.Length];
        var length = 0;
        foreach (var character in phone)
        {
            var normalized = character switch
            {
                >= '٠' and <= '٩' => (char)('0' + character - '٠'),
                >= '۰' and <= '۹' => (char)('0' + character - '۰'),
                _ => character,
            };
            if (normalized is not ('⁦' or '⁧' or '⁨' or '⁩' or '-' or ' '))
                buffer[length++] = normalized;
        }
        var cleaned = new string(buffer, 0, length);
        foreach (var pair in CountryDialCodes.OrderByDescending(item => item.Value.Length))
        {
            var internationalPrefix = "00" + pair.Value;
            var plusPrefix = "+" + pair.Value;
            if (!cleaned.StartsWith(internationalPrefix, StringComparison.Ordinal) &&
                !cleaned.StartsWith(plusPrefix, StringComparison.Ordinal)) continue;
            var prefixLength = cleaned[0] == '+' ? plusPrefix.Length : internationalPrefix.Length;
            var local = cleaned[prefixLength..];
            cleaned = NoLeadingZeroLocalCountries.Contains(pair.Key) ? local : "0" + local;
            break;
        }
        if (NoLeadingZeroLocalCountries.Contains(country) && country != 12 &&
            cleaned.Length == 9 && cleaned.StartsWith('0'))
            cleaned = cleaned[1..];
        return cleaned;
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
        o.OrderWarehouses.Select(w => new OrderItemDto(w.OrderId, w.WarehouseId, w.Amount, w.UnitPrice ?? 0m)).ToList()
    );

    [GeneratedRegex(@"[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF\uFB50-\uFDFF\uFE70-\uFEFF]")]
    private static partial Regex ArabicRegex();
}
