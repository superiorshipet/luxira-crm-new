namespace Luxira.Api.Features.Orders.DTOs;

public record OrderDto(
    int Id,
    int Country,
    string? State,
    int OrderSource,
    string? SourceName,
    int? ManufacturingCompanyId,
    int DeliveryCompanyId,
    string? DeliveryCompanyName,
    string TelephoneNumber,
    string? SecondTelephoneNumber,
    string CustomerName,
    string? Notes,
    string Address,
    DateTime CreatedDate,
    DateTime? LastEditedDate,
    int OrderStatus,
    string OrderStatusName,
    decimal TotalPrice,
    decimal DeliveryPrice,
    decimal CustomerDeliveryPrice,
    string? ApplicationUserId,
    string? ApplicationUserName,
    bool IsPinned,
    bool IsPaid,
    bool IsDelayed,
    IReadOnlyList<OrderItemDto> Items
);

public record OrderItemDto(
    int OrderId,
    int WarehouseId,
    int Amount,
    decimal UnitPrice
);

public record CreateOrderRequest(
    int Country,
    string? State,
    int OrderSource,
    string? SourceName,
    int? ManufacturingCompanyId,
    int DeliveryCompanyId,
    string TelephoneNumber,
    string? SecondTelephoneNumber,
    string CustomerName,
    string? Notes,
    string Address,
    decimal TotalPrice,
    decimal DeliveryPrice,
    decimal CustomerDeliveryPrice,
    string? ChatUrl,
    List<CreateOrderItemRequest> Items
);

public record CreateOrderItemRequest(
    int WarehouseId,
    int Quantity,
    decimal Price,
    decimal? Cost
);

public record UpdateOrderRequest(
    int? Country,
    string? State,
    string? CustomerName,
    string? TelephoneNumber,
    string? SecondTelephoneNumber,
    string? Address,
    string? Notes,
    int? DeliveryCompanyId,
    decimal? TotalPrice,
    decimal? DeliveryPrice,
    decimal? CustomerDeliveryPrice
);

public record UpdateOrderStatusRequest(
    int NewStatus,
    string? Reason,
    string? Note
);

public record BatchUpdateOrderStatusRequest(
    List<int> OrderIds,
    int NewStatus,
    string? Reason,
    string? Note
);

public record OrderFilterRequest(
    string? Search = null,
    int? Status = null,
    int? Country = null,
    int? DeliveryCompanyId = null,
    int? ManufacturingCompanyId = null,
    string? UserId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? PaymentMethod = null,
    int Page = 1,
    int PageSize = 50
);

public record OrderListResult(
    IReadOnlyList<OrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record OrderStatsDto(
    int TotalOrders,
    int NewOrders,
    int DeliveredOrders,
    int ReturnedOrders,
    int CancelledOrders,
    decimal TotalRevenue
);
