using Luxira.Api.Data;
using Luxira.Api.Utils.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.BackgroundServices;

public class ScreenRecordCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScreenRecordCleanupService> _logger;

    public ScreenRecordCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScreenRecordCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = IstanbulTimeHelper.Now;
            // Run at 03:00 AM Istanbul time daily
            if (now.Hour == 3 && now.Minute < 5)
            {
                try
                {
                    await CleanupOldRecordsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean up old screen records.");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CleanupOldRecordsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Remove local/temporary records older than 60 days
        var cutoff = IstanbulTimeHelper.Now.AddDays(-60);
        var oldRecords = await db.ScreenRecords
            .Where(r => r.CreatedAt < cutoff)
            .Take(100)
            .ToListAsync(ct);

        if (oldRecords.Count > 0)
        {
            db.ScreenRecords.RemoveRange(oldRecords);
            await db.SaveChangesAsync(ct);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Cleaned up {Count} expired screen records.", oldRecords.Count);
            }
        }
    }
}
