using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Luxira.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController(ApplicationDbContext context, IConfiguration configuration) : ControllerBase
{
    private const int AggregateRowCap = 20_000;
    private static readonly ConcurrentDictionary<string, bool> ProfilingOverrides = new(StringComparer.OrdinalIgnoreCase);

    [HttpGet("system-info")]
    [HttpGet("/Diagnostics/SystemInfo")]
    public IActionResult GetSystemInfo()
    {
        var process = Process.GetCurrentProcess();
        return Ok(new
        {
            framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), memoryUsageMB = process.WorkingSet64 / 1_048_576,
            threadCount = process.Threads.Count, uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime(), timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("logs")]
    [HttpGet("/logs")]
    public async Task<IActionResult> GetLogs(string? key, string? level, string? levels, string? types, string? kinds, string? category, string? search, int take = 100, int skip = 0, CancellationToken ct = default)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var query = context.AppLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(level)) query = query.Where(item => item.Level == level);
        var levelList = SplitCsv(levels); if (levelList.Length > 0) query = query.Where(item => levelList.Contains(item.Level));
        var typeList = SplitCsv(types); if (typeList.Length > 0) query = query.Where(item => item.Type != null && typeList.Contains(item.Type));
        var kindList = SplitCsv(kinds); if (kindList.Length > 0) query = query.Where(item => item.Kind != null && kindList.Contains(item.Kind));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(item => item.Category == category);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(item => item.Message.Contains(search) || (item.Exception != null && item.Exception.Contains(search)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(item => item.Id).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        return Ok(new { total, rows });
    }

    [HttpGet("logs/facets")]
    [HttpGet("/logs/facets")]
    public async Task<IActionResult> GetLogFacets(string? key, int sinceMinutes = 1440, CancellationToken ct = default)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var since = DateTime.UtcNow.AddMinutes(-Math.Clamp(sinceMinutes, 1, 100_800));
        var rows = await context.AppLogs.AsNoTracking().Where(item => item.CreatedAtUtc >= since)
            .GroupBy(item => new { item.Level, item.Type, item.Kind, item.Category }).Select(group => new { group.Key.Level, group.Key.Type, group.Key.Kind, group.Key.Category, count = group.Count() }).ToListAsync(ct);
        return Ok(new { rows });
    }

    [HttpDelete("logs")]
    [HttpDelete("/logs")]
    public async Task<IActionResult> ClearLogs(string? key, CancellationToken ct)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var removed = await context.AppLogs.ExecuteDeleteAsync(ct);
        return Ok(new { success = true, removed });
    }

    [HttpGet("metrics")]
    [HttpGet("/metrics")]
    public async Task<IActionResult> GetMetrics(string? key, string? kinds, DateTime? fromUtc, DateTime? toUtc, int take = 500, CancellationToken ct = default)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var query = context.AppMetrics.AsNoTracking().AsQueryable();
        var kindList = SplitCsv(kinds); if (kindList.Length > 0) query = query.Where(item => kindList.Contains(item.Kind));
        if (fromUtc.HasValue) query = query.Where(item => item.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(item => item.CreatedAtUtc <= toUtc.Value);
        var rows = await query.OrderByDescending(item => item.Id).Take(Math.Clamp(take, 1, 2_000)).ToListAsync(ct);
        return Ok(new { rows });
    }

    [HttpGet("metrics/kinds")]
    [HttpGet("/metrics/kinds")]
    public async Task<IActionResult> GetMetricKinds(string? key, int sinceMinutes = 1440, CancellationToken ct = default)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var since = DateTime.UtcNow.AddMinutes(-Math.Clamp(sinceMinutes, 1, 100_800));
        return Ok(await context.AppMetrics.AsNoTracking().Where(item => item.CreatedAtUtc >= since)
            .GroupBy(item => item.Kind).Select(group => new { kind = group.Key, count = group.Count() }).OrderByDescending(item => item.count).ToListAsync(ct));
    }

    [HttpGet("metrics/summary")]
    [HttpGet("/metrics/summary")]
    public async Task<IActionResult> GetMetricsSummary(string? key, string? kinds, int sinceMinutes = 1440, CancellationToken ct = default)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var since = DateTime.UtcNow.AddMinutes(-Math.Clamp(sinceMinutes, 1, 100_800));
        var kindList = SplitCsv(kinds);
        var query = context.AppMetrics.AsNoTracking().Where(item => item.CreatedAtUtc >= since);
        if (kindList.Length > 0) query = query.Where(item => kindList.Contains(item.Kind));
        var rows = await query.OrderByDescending(item => item.Id).Take(AggregateRowCap).Select(item => new { item.Kind, item.DurationMs }).ToListAsync(ct);
        var summary = rows.GroupBy(item => item.Kind).Select(group =>
        {
            var values = group.Select(item => item.DurationMs).Order().ToArray();
            return new { kind = group.Key, count = values.Length, averageMs = values.Average(), p50Ms = Percentile(values, .5), p95Ms = Percentile(values, .95), maxMs = values[^1] };
        });
        return Ok(new { rows = summary, truncated = rows.Count == AggregateRowCap });
    }

    [HttpGet("metrics/series")]
    [HttpGet("/metrics/series")]
    public async Task<IActionResult> GetMetricsSeries(string? key, string? kinds, int sinceMinutes = 1440, int bucketMinutes = 5, CancellationToken ct = default)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var since = DateTime.UtcNow.AddMinutes(-Math.Clamp(sinceMinutes, 1, 100_800));
        var kindList = SplitCsv(kinds);
        var query = context.AppMetrics.AsNoTracking().Where(item => item.CreatedAtUtc >= since);
        if (kindList.Length > 0) query = query.Where(item => kindList.Contains(item.Kind));
        var rows = await query.OrderByDescending(item => item.Id).Take(AggregateRowCap).Select(item => new { item.Kind, item.CreatedAtUtc, item.DurationMs }).ToListAsync(ct);
        bucketMinutes = Math.Clamp(bucketMinutes, 1, 1440);
        var series = rows.GroupBy(item => new { item.Kind, Bucket = new DateTime(item.CreatedAtUtc.Ticks - item.CreatedAtUtc.Ticks % TimeSpan.FromMinutes(bucketMinutes).Ticks, DateTimeKind.Utc) })
            .Select(group => new { kind = group.Key.Kind, at = group.Key.Bucket, count = group.Count(), averageMs = group.Average(item => item.DurationMs), maxMs = group.Max(item => item.DurationMs) }).OrderBy(item => item.at);
        return Ok(new { rows = series, truncated = rows.Count == AggregateRowCap });
    }

    [HttpDelete("metrics")]
    [HttpDelete("/metrics")]
    public async Task<IActionResult> ClearMetrics(string? key, string? kinds, CancellationToken ct)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var kindList = SplitCsv(kinds);
        var query = context.AppMetrics.AsQueryable();
        if (kindList.Length > 0) query = query.Where(item => kindList.Contains(item.Kind));
        return Ok(new { success = true, removed = await query.ExecuteDeleteAsync(ct) });
    }

    [HttpGet("profiling")]
    [HttpGet("/profiling")]
    public IActionResult GetProfiling(string? key)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        var enabled = ProfilingOverrides.GetValueOrDefault("enabled", configuration.GetValue<bool>("Diagnostics:ProfilingEnabled"));
        return Ok(new { profilingEnabled = enabled, slowQueryThresholdMs = configuration.GetValue<int?>("Diagnostics:SlowQueryThresholdMs") ?? 250 });
    }

    [HttpPost("profiling")]
    [HttpPost("/profiling")]
    public IActionResult SetProfiling(string? key, [FromBody] ProfilingRequest request)
    {
        if (!IsAuthorized(key)) return Unauthorized(new { message = "invalid or missing key" });
        ProfilingOverrides["enabled"] = request.Enabled;
        return Ok(new { success = true, profilingEnabled = request.Enabled });
    }

    private bool IsAuthorized(string? key)
    {
        var expected = configuration["Diagnostics:LogsApiKey"];
        return string.IsNullOrWhiteSpace(expected) ? User.Identity?.IsAuthenticated == true : CryptographicEquals(key, expected);
    }

    private static bool CryptographicEquals(string? left, string right)
    {
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
    private static string[] SplitCsv(string? value) => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static double Percentile(double[] sorted, double percentile) => sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(percentile * sorted.Length) - 1];
}

public sealed record ProfilingRequest(bool Enabled);
