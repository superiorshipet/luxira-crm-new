using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.Orders.Services;

public class OrderService
{
    private readonly OrderRepository _repository;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrderRepository repository, ILogger<OrderService> logger)
    {
        _repository = repository;
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
            OrderStatus = 1, // طلب_جديد
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

        var created = await _repository.CreateAsync(order, ct);

        // Record initial status history
        await _repository.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = created.Id,
            OldStatus = 0,
            NewStatus = 1,
            UserId = userId,
            Note = "Order Created"
        }, ct);

        return MapToDto(created);
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusRequest request, string userId, CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(orderId, ct);
        if (order == null)
        {
            throw new NotFoundException($"Order with ID {orderId} was not found.");
        }

        int oldStatus = order.OrderStatus;
        if (oldStatus == request.NewStatus)
        {
            return MapToDto(order);
        }

        order.OrderStatus = request.NewStatus;
        order.LastEditedDate = DateTime.UtcNow;
        order.Editedby = userId;

        await _repository.UpdateAsync(order, ct);

        await _repository.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            OldStatus = oldStatus,
            NewStatus = request.NewStatus,
            UserId = userId,
            Reason = request.Reason,
            Note = request.Note
        }, ct);

        return MapToDto(order);
    }

    public async Task<int> BatchUpdateOrderStatusAsync(BatchUpdateOrderStatusRequest request, string userId, CancellationToken ct = default)
    {
        int updatedCount = 0;
        foreach (var orderId in request.OrderIds)
        {
            try
            {
                await UpdateOrderStatusAsync(orderId, new UpdateOrderStatusRequest(request.NewStatus, request.Reason, request.Note), userId, ct);
                updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update order {OrderId} in batch update", orderId);
            }
        }

        return updatedCount;
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
        GetStatusDisplayName(o.OrderStatus),
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

    private static string GetStatusDisplayName(int status) => status switch
    {
        1 => "طلب جديد",
        2 => "مؤكد",
        3 => "قيد التجهيز",
        4 => "جاهز للتسليم",
        5 => "تم التوصيل",
        6 => "مؤجل",
        7 => "مرتجع",
        8 => "فشل التوصيل",
        9 => "ملغي",
        _ => "غير معروف"
    };
}
