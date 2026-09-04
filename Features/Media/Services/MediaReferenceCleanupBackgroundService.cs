using Luxira.Api.Data;
using Luxira.Api.Utils.Time;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Media.Services;

/// <summary>Runs the guarded dead-reference sweep once per Istanbul day after 09:00.</summary>
public sealed class MediaReferenceCleanupBackgroundService : BackgroundService
{
    private const int RunAtHour = 9;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MediaReferenceCleanupBackgroundService> _logger;

    public MediaReferenceCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MediaReferenceCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await DelayAsync(StartupDelay, stoppingToken)) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                if (await IsDueAsync(scope.ServiceProvider, stoppingToken))
                {
                    var cleanup = scope.ServiceProvider.GetRequiredService<MediaReferenceCleanupService>();
                    var run = await cleanup.RunAsync("auto", stoppingToken);
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation(
                            "Media cleanup {Id}: dry-run {DryRun}, scanned {Scanned}, eligible {Eligible}, cleared {Cleared}, aborted {Aborted}.",
                            run.Id, run.IsDryRun, run.RowsScanned, run.WouldClearCount, run.ReferencesCleared, run.WasAborted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Media reference cleanup pass failed.");
            }

            if (!await DelayAsync(PollInterval, stoppingToken)) return;
        }
    }

    internal static async Task<bool> IsDueAsync(IServiceProvider services, CancellationToken ct)
    {
        var now = IstanbulTimeHelper.Now;
        if (now.Hour < RunAtHour) return false;

        var db = services.GetRequiredService<ApplicationDbContext>();
        var startOfDay = now.Date;
        var startOfNextDay = startOfDay.AddDays(1);
        return !await db.MediaReferenceCleanupRuns.AsNoTracking().AnyAsync(
            run => run.TriggeredBy == "auto" && run.StartedAt >= startOfDay && run.StartedAt < startOfNextDay,
            ct);
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
