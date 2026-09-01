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

        return NotImplemented("A durable application-log query source is not configured.");
    }

    [HttpGet("logs/facets")]
    public IActionResult GetLogFacets([FromQuery] string? key)
    {
        return NotImplemented("Log facets require a durable application-log query source.");
    }

    [HttpGet("metrics/p95")]
    public IActionResult GetP95Metrics([FromQuery] string? key)
    {
        return NotImplemented("Request percentile storage is not configured.");
    }

    [HttpGet("metrics/timeseries")]
    public IActionResult GetTimeseriesMetrics([FromQuery] string? key)
    {
        return NotImplemented("Request time-series storage is not configured.");
    }

    [HttpGet("profiling")]
    public IActionResult GetProfilingStatus([FromQuery] string? key)
    {
        return Ok(new
        {
            profilingEnabled = _configuration.GetValue<bool>("Diagnostics:ProfilingEnabled"),
            slowQueryThresholdMs = _configuration.GetValue<int?>("Diagnostics:SlowQueryThresholdMs") ?? 250,
            detailedErrors = _configuration.GetValue<bool>("Diagnostics:DetailedErrors")
        });
    }

    [HttpPost("profiling")]
    public IActionResult UpdateProfiling([FromQuery] string? key, [FromBody] object payload)
    {
        return NotImplemented("Runtime profiling configuration changes are disabled; use deployment configuration.");
    }

    private ObjectResult NotImplemented(string detail) => StatusCode(
        StatusCodes.Status501NotImplemented,
        new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Operation not implemented",
            Detail = detail
        });
}
