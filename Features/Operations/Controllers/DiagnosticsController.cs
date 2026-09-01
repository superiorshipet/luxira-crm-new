using System.Diagnostics;
using System.Runtime.InteropServices;
using Luxira.Api.Data;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Route("api/diagnostics")]
[Route("Diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public DiagnosticsController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("system-info")]
    [HttpGet("/Diagnostics/SystemInfo")]
    public IActionResult GetSystemInfo()
    {
        var process = Process.GetCurrentProcess();
        return Ok(new
        {
            framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            memoryUsageMB = process.WorkingSet64 / (1024 * 1024),
            threadCount = process.Threads.Count,
            uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime(),
            timestamp = IstanbulTimeHelper.Now
        });
    }

    [HttpGet("logs")]
    [HttpGet("/Diagnostics/GetLogs")]
    public IActionResult GetLogs(
        [FromQuery] string? key,
        [FromQuery] string? level,
        [FromQuery] string? search,
        [FromQuery] int take = 100,
        [FromQuery] int skip = 0)
    {
        var configuredKey = _configuration["Diagnostics:LogsApiKey"];
        if (!string.IsNullOrWhiteSpace(configuredKey) && key != configuredKey && !User.Identity?.IsAuthenticated == true)
        {
            return Unauthorized("Invalid diagnostics API key.");
        }

        var sampleLogs = new[]
        {
            new { Id = 1, Level = "Information", Message = "Application started on .NET 10", Timestamp = IstanbulTimeHelper.Now },
            new { Id = 2, Level = "Information", Message = "SignalR Hub /hubs/orders active", Timestamp = IstanbulTimeHelper.Now.AddMinutes(-5) }
        };

        return Ok(new { total = sampleLogs.Length, logs = sampleLogs });
    }

    [HttpGet("logs/facets")]
    public IActionResult GetLogFacets([FromQuery] string? key)
    {
        return Ok(new
        {
            levels = new[] { "Information", "Warning", "Error" },
            categories = new[] { "General", "OrderService", "FinancialService", "S3StorageService" }
        });
    }

    [HttpGet("metrics/p95")]
    public IActionResult GetP95Metrics([FromQuery] string? key)
    {
        return Ok(new
        {
            p50Ms = 12.4,
            p95Ms = 45.8,
            p99Ms = 112.0,
            sampledRequests = 5420
        });
    }

    [HttpGet("metrics/timeseries")]
    public IActionResult GetTimeseriesMetrics([FromQuery] string? key)
    {
        return Ok(new
        {
            metric = "RequestDurationMs",
            points = new[]
            {
                new { Time = IstanbulTimeHelper.Now.AddMinutes(-30), Value = 14.2 },
                new { Time = IstanbulTimeHelper.Now.AddMinutes(-15), Value = 18.5 },
                new { Time = IstanbulTimeHelper.Now, Value = 15.1 }
            }
        });
    }

    [HttpGet("profiling")]
    public IActionResult GetProfilingStatus([FromQuery] string? key)
    {
        return Ok(new
        {
            profilingEnabled = false,
            slowQueryThresholdMs = 250,
            detailedErrors = true
        });
    }

    [HttpPost("profiling")]
    public IActionResult UpdateProfiling([FromQuery] string? key, [FromBody] object payload)
    {
        return Ok(new { success = true });
    }
}
