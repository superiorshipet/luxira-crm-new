using Luxira.Api.Data;
using Luxira.Api.Utils.Time;
using Luxira.Api.Features.Media.Services;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Features.Employees.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.BackgroundServices;

public class ScreenRecordCleanupService : BackgroundService
{
    private static readonly MediaColumnSpec ScreenRecordSpec =
        MediaModuleRegistry.Find("screen-records")!.Columns[0];
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

        var cutoff = IstanbulTimeHelper.Now.AddDays(-30);
        var oldRecords = await db.ScreenRecords
            .Where(record => record.Date < cutoff)
            .Take(100)
            .ToListAsync(ct);

        if (oldRecords.Count == 0) return;

        var storage = scope.ServiceProvider.GetRequiredService<S3StorageService>();
        var root = Path.GetFullPath(scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>().WebRootPath);
        var removable = new List<ScreenRecord>();

        foreach (var record in oldRecords)
        {
            var key = record.VideoS3Key ?? MediaModuleRegistry.TryExtractKey(record.VideoPath);
            if (key is not null)
            {
                try { await storage.DeleteAsync(key, userId: null, ct); }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to delete retained screen recording {Key}; database row kept.", key);
                    continue;
                }
            }

            var relative = key is null
                ? record.VideoPath.TrimStart('/', '\\')
                : MediaModuleRegistry.DeriveRelativePath(ScreenRecordSpec, key);
            if (!string.IsNullOrWhiteSpace(relative))
            {
                try
                {
                    var fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                    var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
                    if (fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)) File.Delete(fullPath);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to delete local retained screen recording {Path}; database row kept.", record.VideoPath);
                    continue;
                }
            }

            removable.Add(record);
        }

        db.ScreenRecords.RemoveRange(removable);
        await db.SaveChangesAsync(ct);
        if (removable.Count > 0 && _logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Cleaned up {Count} expired screen records.", removable.Count);
    }
}
