using Luxira.Api.Data;
using Luxira.Api.Infrastructure.Email;
using Luxira.Api.Features.Orders.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.BackgroundServices;

public sealed class PendingDownloadReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingDownloadReminderBackgroundService> _logger;

    public PendingDownloadReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingDownloadReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(10000, stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPendingDownloadsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PendingDownloadReminder background service.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckPendingDownloadsAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<LuxiraEmailService>();

        var pendingOrdersCount = await db.Orders
            .Where(o => o.OrderStatus == OrderStatusCodes.New)
            .CountAsync(token);

        if (pendingOrdersCount > 50)
        {
            _logger.LogWarning("Found {Count} pending new orders. Sending reminder email...", pendingOrdersCount);
            await emailService.SendEmailAsync(
                string.Empty,
                $"تنبيه: يوجد {pendingOrdersCount} طلب بانتظار التجهيز",
                $"<p>يرجى الانتباه، هناك <strong>{pendingOrdersCount}</strong> طلبات جديدة لم يتم تنزيلها وتجهيزها بعد.</p>",
                null,
                null,
                token
            );
        }
    }
}
