using Luxira.Api.Data;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Services;

public sealed class CamexReconciliationService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<CamexReconciliationService> logger) : BackgroundService
{
    private static readonly int[] TerminalStates = [6, 11, 12, 16];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { if (configuration.GetValue<bool?>("Camex:Enabled") ?? false) await RunPass(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "CAMEX reconciliation pass failed"); }
            try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue<int?>("Camex:ReconcileIntervalMinutes") ?? 360)), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunPass(CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var courier = scope.ServiceProvider.GetRequiredService<CourierDispatchService>(); var orders = scope.ServiceProvider.GetRequiredService<OrderService>(); var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, configuration.GetValue<int?>("Camex:ReconcileMaxAgeDays") ?? 60));
        var rows = await context.Orders.AsNoTracking().Where(item => item.CamexTrackingNumber != null && (item.CamexState == null || !TerminalStates.Contains(item.CamexState.Value)) && (item.CamexStateChangedAt ?? item.CamexConfirmedAt) > cutoff).OrderBy(item => item.CamexStateChangedAt ?? item.CamexConfirmedAt).Take(40).Select(item => new { item.Id, Tracking = item.CamexTrackingNumber!.Value, item.CamexState, item.OrderStatus }).ToListAsync(ct);
        foreach (var row in rows) { var state = await courier.GetCamexStateAsync(row.Tracking, ct); if (!state.HasValue || state == row.CamexState || state == 0 && row.CamexState.HasValue) continue; var (target, advisory) = Map(state.Value); var now = DateTime.UtcNow; await context.Orders.Where(item => item.Id == row.Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.CamexState, state).SetProperty(item => item.CamexStateChangedAt, now).SetProperty(item => item.CamexAdvisoryState, advisory is null ? null : state).SetProperty(item => item.CamexAdvisoryAt, advisory is null ? null : now).SetProperty(item => item.CamexAdvisoryNote, advisory), ct); if (target.HasValue && target != row.OrderStatus) await orders.UpdateOrderStatusAsync(row.Id, new UpdateOrderStatusRequest(target.Value, $"Camex reconciliation state {state.Value}", advisory), OrderStatusActor.TrustedSystem("camex-reconciliation"), ct); logger.LogWarning("CAMEX reconciliation corrected order {OrderId} to state {State}", row.Id, state); }
    }

    public static (int? Status, string? Advisory) Map(int state) => state switch { 0 or 3 => (OrderStatusCodes.Prepared, null), 4 or 5 or 18 => (OrderStatusCodes.InDelivery, null), 6 => (OrderStatusCodes.Delivered, null), 8 or 9 or 11 or 16 => (OrderStatusCodes.FailedDelivery, null), 20 => (OrderStatusCodes.Suspicious, "Change Request - an edit to the shipment is pending at CAMEX"), 12 => (null, null), _ => (null, $"حالة كامكس غير معروفة: {state}") };
}
