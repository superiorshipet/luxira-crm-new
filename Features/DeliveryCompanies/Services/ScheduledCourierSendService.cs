using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Services;

public sealed class ScheduledCourierSendService(IServiceScopeFactory scopes, ILogger<ScheduledCourierSendService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunPass(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunPass(stoppingToken);
    }

    private async Task RunPass(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dispatch = scope.ServiceProvider.GetRequiredService<CourierDispatchService>();
            var now = DateTime.UtcNow;
            var staleCutoff = now.AddMinutes(-15);
            var reaped = await context.ScheduledSendRequests.Where(item => item.Status == ScheduledSendStatus.Firing && item.ClaimedAtUtc < staleCutoff)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ScheduledSendStatus.Failed).SetProperty(item => item.CompletedAtUtc, now).SetProperty(item => item.ResultSummary, "Abandoned after restart while sending; verify courier state before rescheduling."), ct);
            if (reaped > 0) logger.LogWarning("Released {Count} stale courier schedules", reaped);
            var dueIds = await context.ScheduledSendRequests.AsNoTracking().Where(item => item.Status == ScheduledSendStatus.Pending && item.FireAtUtc <= DateTime.UtcNow).OrderBy(item => item.FireAtUtc).Select(item => item.Id).Take(5).ToListAsync(ct);
            foreach (var id in dueIds)
            {
                var claimedAt = DateTime.UtcNow;
                var claimed = await context.ScheduledSendRequests.Where(item => item.Id == id && item.Status == ScheduledSendStatus.Pending)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ScheduledSendStatus.Firing).SetProperty(item => item.ClaimedAtUtc, claimedAt), ct);
                if (claimed == 0) continue;
                var schedule = await context.ScheduledSendRequests.AsNoTracking().FirstAsync(item => item.Id == id, ct);
                try
                {
                    var orderIds = await dispatch.PendingOrders(schedule.CourierKey).OrderBy(item => item.CreatedDate).Select(item => item.Id).Take(schedule.OrderCount).ToListAsync(ct);
                    var sent = 0; var blocked = 0; var deferred = 0;
                    foreach (var orderId in orderIds) { var result = await dispatch.ConfirmAsync(schedule.CourierKey, orderId, ct); if (result.Outcome == CourierConfirmOutcome.Sent) sent++; else if (result.Outcome == CourierConfirmOutcome.Blocked) blocked++; else deferred++; }
                    var summary = $"sent={sent}; blocked={blocked}; deferred={deferred}; orders={string.Join(',', orderIds)}";
                    await context.ScheduledSendRequests.Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ScheduledSendStatus.Completed).SetProperty(item => item.CompletedAtUtc, DateTime.UtcNow).SetProperty(item => item.ResultSummary, summary), ct);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Scheduled courier send {ScheduleId} failed", id);
                    await context.ScheduledSendRequests.Where(item => item.Id == id).ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, ScheduledSendStatus.Failed).SetProperty(item => item.CompletedAtUtc, DateTime.UtcNow).SetProperty(item => item.ResultSummary, exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message), ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogError(exception, "Scheduled courier send pass failed"); }
    }
}
