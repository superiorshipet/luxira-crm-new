using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
public sealed class ExternalOrderApiController : ControllerBase
{
    private static readonly int[] PublicTrackingStatuses =
        [OrderStatusCodes.New, OrderStatusCodes.Prepared, OrderStatusCodes.InDelivery, OrderStatusCodes.Delivered, OrderStatusCodes.FailedDelivery];
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<OrderHub> _hub;
    private readonly S3StorageService _s3;

    public ExternalOrderApiController(ApplicationDbContext db, IHubContext<OrderHub> hub, S3StorageService s3)
    {
        _db = db;
        _hub = hub;
        _s3 = s3;
    }

    [HttpPost("/Api/Order/UpdateStatus")]
    public async Task<IActionResult> UpdateStatus([FromBody] ExternalStatusUpdateRequest request, CancellationToken ct)
    {
        var externalIds = request.OrderIds?.Where(id => id > 0).Distinct().ToArray() ?? [];
        if (externalIds.Length == 0) return Ok("No order IDs provided.");
        if (!OrderStatusCodes.IsDefined(request.NewStatus)) return BadRequest("Invalid order status.");

        var orders = await _db.Orders.AsNoTracking()
            .Where(order => order.ExternalOrderId.HasValue && externalIds.Contains(order.ExternalOrderId.Value))
            .Select(order => new { order.Id, order.DeliveryCompanyId, PreviousStatus = order.OrderStatus })
            .ToListAsync(ct);
        if (orders.Count == 0) return Ok("No matching orders found.");

        var orderIds = orders.Select(order => order.Id).ToArray();
        var now = IstanbulTimeHelper.Now;
        var actorId = User.GetUserId();
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.Orders.Where(order => orderIds.Contains(order.Id))
            .ExecuteUpdateAsync(update => update
                .SetProperty(order => order.OrderStatus, request.NewStatus)
                .SetProperty(order => order.LastEditedDate, now)
                .SetProperty(order => order.Editedby, actorId), ct);
        var histories = orders.Select(order => new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = request.NewStatus,
            Reason = request.Reason,
            CreatedAt = now,
            ApplicationUserId = actorId,
            Name = $"PreviousStatus:{order.PreviousStatus}",
            IsHidden = false
        }).ToList();
        _db.OrderStatusHistories.AddRange(histories);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var userName = actorId is null
            ? "Unknown"
            : await _db.Users.AsNoTracking().Where(user => user.Id == actorId).Select(user => user.Name).FirstOrDefaultAsync(ct) ?? "Unknown";
        await Task.WhenAll(orders.Zip(histories).Select(pair => BroadcastAsync(
            pair.First.Id,
            pair.First.DeliveryCompanyId,
            pair.Second,
            userName,
            ct)));
        return Ok(new { success = true, message = "Order status updated successfully." });
    }

    [HttpGet("/Api/Order/ShipmentTracking/{orderId:int}")]
    public async Task<IActionResult> ShipmentTracking([RouteOrRequest] int orderId, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Where(item => item.Id == orderId)
            .Select(item => new { item.Id, item.Country, item.State, item.ManufacturingCompanyId })
            .FirstOrDefaultAsync(ct);
        if (order is null) return NotFound("Order not found.");
        if (order.ManufacturingCompanyId != 1) return BadRequest("Wrong order.");

        var warehouses = await (
            from item in _db.OrderWarehouses.AsNoTracking()
            join warehouse in _db.Warehouses.AsNoTracking() on item.WarehouseId equals warehouse.Id
            join product in _db.MainWarehouses.AsNoTracking() on warehouse.MainWarehouseId equals product.Id into productJoin
            from product in productJoin.DefaultIfEmpty()
            where item.OrderId == orderId
            select new { warehouse.Name, ImageUrl = product == null ? null : product.ImageUrl, ImageS3Key = product == null ? null : product.ImageS3Key, item.Amount }
        ).ToListAsync(ct);
        var warehouseDetails = warehouses.Select(item => new
        {
            warehouseName = item.Name,
            imageUrl = !string.IsNullOrWhiteSpace(item.ImageS3Key) ? _s3.GetPresignedUrl(item.ImageS3Key, 120) : item.ImageUrl,
            amount = item.Amount
        });
        var histories = await _db.OrderStatusHistories.AsNoTracking()
            .Where(history => history.OrderId == orderId && history.Status.HasValue && PublicTrackingStatuses.Contains(history.Status.Value))
            .GroupBy(history => history.Status)
            .Select(group => group.OrderByDescending(history => history.CreatedAt).First())
            .OrderBy(history => history.CreatedAt)
            .Select(history => new
            {
                createdAt = history.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                status = history.Status.ToString(),
                history.Reason
            })
            .ToListAsync(ct);
        if (histories.Count == 0) return NotFound();
        return Ok(new { warehouseDetails, orderStatusHistory = histories, country = order.Country.ToString(), city = order.State, orderId });
    }

    private async Task BroadcastAsync(int orderId, int deliveryCompanyId, OrderStatusHistory history, string userName, CancellationToken ct)
    {
        var payload = new
        {
            OrderId = orderId,
            StatusHistoryId = history.Id,
            Status = history.Status,
            history.CreatedAt,
            history.ApplicationUserId,
            history.Reason,
            UserName = userName,
            StatusPhrase = OrderStatusCodes.GetDisplayName(history.Status ?? OrderStatusCodes.Unknown),
            ColorStyle = string.Empty
        };
        await Task.WhenAll(
            _hub.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", payload, ct),
            _hub.Clients.Group($"deliveryCompany_{deliveryCompanyId}").SendAsync("OrderStatusUpdated", payload, ct));
    }
}

public sealed record ExternalStatusUpdateRequest(IReadOnlyCollection<int>? OrderIds, int NewStatus, string? Reason);
