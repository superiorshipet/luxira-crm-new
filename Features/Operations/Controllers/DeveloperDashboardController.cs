using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/operations/developer-dashboard")]
[Route("DeveloperDashboard")]
[Route("Diagnostics")]
public class DeveloperDashboardController : ControllerBase
{
    [HttpGet]
    [HttpGet("/DeveloperDashboard/GetStatus")]
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
            uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime()
        });
    }
}
