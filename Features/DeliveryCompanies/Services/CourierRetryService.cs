using Luxira.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Services;

public sealed class CourierRetryService(IServiceScopeFactory scopes, ILogger<CourierRetryService> logger) : BackgroundService
{
    private const int MaximumAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await RunPass(stoppingToken);
    }

    private async Task RunPass(CancellationToken ct)
    {
        try
        {
            foreach (var courier in new[] { "sandoog", "camex" })
            {
                using var scope = scopes.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var dispatch = scope.ServiceProvider.GetRequiredService<CourierDispatchService>();
                if (!dispatch.IsConfigured(courier)) continue;
                var cutoff = DateTime.UtcNow.AddMinutes(-2);
                var ids = await dispatch.RetryCandidates(courier, cutoff, MaximumAttempts).AsNoTracking().OrderBy(item => courier == "sandoog" ? item.SandoogLastAttemptAt : item.CamexLastAttemptAt).Select(item => item.Id).Take(10).ToListAsync(ct);
                foreach (var id in ids) await dispatch.RetryAsync(courier, id, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogError(exception, "Courier retry pass failed"); }
    }
}
