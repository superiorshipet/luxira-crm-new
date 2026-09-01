using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Repositories;
using Luxira.Api.Utils.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Services;

public class OrderService
{
    private readonly OrderRepository _repository;
    private readonly ApplicationDbContext _context;
    private readonly OrderStatusTransitionPolicy _statusTransitionPolicy;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderRepository repository,
        ApplicationDbContext context,
        OrderStatusTransitionPolicy statusTransitionPolicy,
        ILogger<OrderService> logger)
    {
        _repository = repository;
        _context = context;
        _statusTransitionPolicy = statusTransitionPolicy;
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
        {
            throw new BadRequestException("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TelephoneNumber))
        {
            throw new BadRequestException("Telephone number is required.");
        }

        if (request.DeliveryCompanyId <= 0)
        {
            throw new BadRequestException("A valid delivery company must be selected.");
        }

        // Validate Minimum Selling Price if items provided
        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var item in request.Items)
            {
                var minPrice = await _context.ProductMinimumSellingPrices
                    .Where(p => p.Country == request.Country)
                    .Select(p => p.MinimumPrice)
                    .FirstOrDefaultAsync(ct);

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
            TelephoneNumber = request.TelephoneNumber,
            SecondTelephoneNumber = request.SecondTelephoneNumber,
            CustomerName = request.CustomerName,
            Notes = request.Notes,
            Address = request.Address,
            TotalPrice = request.TotalPrice,
            DeliveryPrice = request.DeliveryPrice,
            CustomerDeliveryPrice = request.CustomerDeliveryPrice,
            Chaturl = request.ChatUrl,
            ApplicationUserId = userId,
            OrderStatus = OrderStatusCodes.New,
            CreatedDate = DateTime.UtcNow
        };

        if (request.Items != null && request.Items.Count > 0)
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
            Status = OrderStatusCodes.New,
            ApplicationUserId = userId,
            CreatedAt = DateTime.UtcNow,
            Reason = "Order Created",
            Name = $"PreviousStatus:{OrderStatusCodes.New}",
        });

        var created = await _repository.CreateAsync(order, ct);

        return MapToDto(created);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(
        int orderId,
        UpdateOrderStatusRequest request,
        OrderStatusActor actor,
        CancellationToken ct = default)
    {
        if (!OrderStatusCodes.IsDefined(request.NewStatus))
        {
            throw new BadRequestException($"Order status '{request.NewStatus}' is not part of the legacy status contract.");
        }

        var order = await _repository.GetByIdAsync(orderId, ct);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {orderId} was not found.");
        }

        _statusTransitionPolicy.EnsureAllowed(
            order,
            request.NewStatus,
            request.Reason,
            actor);

        int oldStatus = order.OrderStatus;
        if (oldStatus == request.NewStatus)
        {
            return MapToDto(order);
        }

        order.OrderStatus = request.NewStatus;
        order.LastEditedDate = DateTime.UtcNow;
        order.Editedby = actor.UserId;

        if (request.NewStatus == OrderStatusCodes.Delivered && !order.IsBonusPaidForEmployee)
        {
            var bonusConfig = await _context.OrderBonusConfigurations
                .FirstOrDefaultAsync(b => b.Country == order.Country && b.IsActive, ct);

            if (bonusConfig != null && bonusConfig.BonusAmount > 0)
            {
                order.IsBonus = true;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Order {OrderId} qualified for bonus {BonusAmount}", order.Id, bonusConfig.BonusAmount);
                }
            }
        }

        await _repository.UpdateWithStatusHistoryAsync(order, new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = request.NewStatus,
            ApplicationUserId = actor.UserId,
            CreatedAt = DateTime.UtcNow,
            Reason = CombineHistoryReason(request.Reason, request.Note),
            Name = $"PreviousStatus:{oldStatus}",
        }, ct);

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
        int updatedCount = 0;
        foreach (var orderId in request.OrderIds)
        {
            try
            {
                await UpdateOrderStatusAsync(
                    orderId,
                    new UpdateOrderStatusRequest(
                        request.NewStatus,
                        request.Reason,
                        request.Note),
                    actor,
                    ct);
                updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update order {OrderId} in batch update", orderId);
            }
        }

        return updatedCount;
    }

    public async Task<OrderDto> UpdateInlineFieldAsync(int orderId, string fieldName, string? newValue, string userId, CancellationToken ct = default)
    {
        var order = await _context.Orders.Include(o => o.OrderWarehouses).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {orderId} was not found.");
        }

        var nextEditNumber = (await _context.OrderEditHistories
            .Where(history => history.OrderId == order.Id)
            .MaxAsync(history => (int?)history.EditNumber, ct) ?? 0) + 1;
        var editHistory = CreateEditSnapshot(
            order,
            nextEditNumber,
            userId);

        var lowerField = fieldName.ToLowerInvariant();
        switch (lowerField)
        {
            case "customername":
                order.CustomerName = newValue ?? string.Empty;
                break;
            case "telephonenumber":
            case "phone":
                order.TelephoneNumber = newValue ?? string.Empty;
                break;
            case "secondtelephonenumber":
                order.SecondTelephoneNumber = newValue;
                break;
            case "address":
                order.Address = newValue ?? string.Empty;
                break;
            case "notes":
            case "note":
                order.Notes = newValue;
                break;
            case "state":
            case "city":
                order.State = newValue;
                break;
            case "deliverycompanyid":
                if (int.TryParse(newValue, out var dcId)) order.DeliveryCompanyId = dcId;
                break;
            case "totalprice":
                if (decimal.TryParse(newValue, out var tp)) order.TotalPrice = tp;
                break;
            case "delegateemployeeid":
                order.DelegateEmployeeId = newValue;
                break;
            default:
                throw new BadRequestException($"Unknown inline field '{fieldName}'.");
        }

        order.LastEditedDate = DateTime.UtcNow;
        order.Editedby = userId;

        // Record edit history
        await _context.OrderEditHistories.AddAsync(editHistory, ct);

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
        var cleanPhone = phoneNumber.Trim();
        var duplicates = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .AsNoTracking()
            .Where(o => o.TelephoneNumber == cleanPhone || o.SecondTelephoneNumber == cleanPhone)
            .OrderByDescending(o => o.CreatedDate)
            .Take(10)
            .ToListAsync(ct);

        return duplicates.Select(MapToDto).ToList();
    }

    public async Task<OrderStatsDto> GetStatsAsync(int? country = null, CancellationToken ct = default)
    {
        return await _repository.GetStatsAsync(country, ct);
    }

    private static OrderDto MapToDto(Order o) => new(
        o.Id,
        o.Country,
        o.State,
        o.OrderSource,
        o.SourceName,
        o.ManufacturingCompanyId,
        o.DeliveryCompanyId,
        o.DeliveryCompany?.Name ?? o.DeliveryCompany?.DisplayName,
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
        o.ApplicationUser?.UserName ?? o.ApplicationUser?.Name,
        o.IsPinned,
        o.IsPaid,
        o.IsDelayed,
        o.OrderWarehouses.Select(w => new OrderItemDto(w.Id, w.WarehouseId, w.Quantity, w.Price, w.Cost)).ToList()
    );

}
