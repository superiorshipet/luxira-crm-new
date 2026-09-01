using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.BackgroundServices;

public sealed class DeliveredToBalanceAutoTransitionBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeliveredToBalanceAutoTransitionBackgroundService> _logger;

    public DeliveredToBalanceAutoTransitionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeliveredToBalanceAutoTransitionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(5000, stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeliveredToBalance auto transition background service.");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deliveredOrders = await db.Orders
            .Include(o => o.DeliveryCompany)
            .Where(o => o.OrderStatus == 5 && o.DeliveryCompany != null && o.DeliveryCompany.AutoConvertDeliveredToBalanceUpdated) // تم_التسليم
            .Take(50)
            .ToListAsync(token);

        if (deliveredOrders.Count == 0) return;

        var now = IstanbulTimeHelper.Now;
        foreach (var order in deliveredOrders)
        {
            order.OrderStatus = 8; // تم_تحديث_الرصيد
            order.LastEditedDate = now;

            db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                OldStatus = 5,
                NewStatus = 8,
                UserId = "AutoTransitionService",
                ChangedAt = now,
                Reason = "Auto Delivered to Balance Updated transition"
            });
        }

        await db.SaveChangesAsync(token);
        _logger.LogInformation("Auto-transitioned {Count} orders from Delivered to BalanceUpdated", deliveredOrders.Count);
    }
}
