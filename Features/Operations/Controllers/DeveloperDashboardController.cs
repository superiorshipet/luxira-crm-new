using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using Luxira.Api.Features.Orders.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,SoftwareDeveloper")]
[Route("api/v1/operations/developer-dashboard")]
[Route("DeveloperDashboard")]
public sealed class DeveloperDashboardController(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IHubContext<OrderHub> hub,
    IWebHostEnvironment environment) : ControllerBase
{
    private const int MaximumManualUrls = 300;
    private static DeployWarningNotice? _activeWarning;
    private static readonly object WarningLock = new();
    private static DateTime? _lastPublishStamp;

    [HttpGet]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken ct) => await Status(ct);

    [HttpGet("Status")]
    [HttpGet("/DeveloperDashboard/GetStatus")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var tokenStatus = await GetCloudflareStatus(ct);
        return Ok(new { success = true, enabled = CloudflareEnabled, token = tokenStatus, publishStamp = ReadPublishStamp(), activeWarning = GetWarning() });
    }

    [HttpGet("/Diagnostics/SystemInfo")]
    public IActionResult GetSystemInfo()
    {
        var process = Process.GetCurrentProcess();
        return Ok(new { framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription, memoryUsageMB = process.WorkingSet64 / 1_048_576, threadCount = process.Threads.Count, uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime() });
    }

    [HttpPost("PurgeEverything")]
    public Task<IActionResult> PurgeEverything(CancellationToken ct) => Purge(new { purge_everything = true }, ct);

    [HttpPost("PurgeUrls")]
    public async Task<IActionResult> PurgeUrls([FromForm] string? urls, CancellationToken ct)
    {
        var parsed = (urls ?? string.Empty).Split(['\n', '\r', ',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaximumManualUrls + 1).ToList();
        if (parsed.Count == 0 || parsed.Count > MaximumManualUrls) return BadRequest(new { success = false, message = $"أدخل من 1 إلى {MaximumManualUrls} رابط." });
        if (parsed.Any(value => !Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))) return BadRequest(new { success = false, message = "كل الروابط يجب أن تبدأ بـ http أو https." });
        foreach (var batch in parsed.Chunk(30))
        {
            var result = await Purge(new { files = batch }, ct);
            if (result is ObjectResult { StatusCode: >= 400 } || result is BadRequestObjectResult) return result;
        }
        return Ok(new { success = true, filesCount = parsed.Count });
    }

    [HttpPost("RecheckPublish")]
    public async Task<IActionResult> RecheckPublish([FromForm] bool force, CancellationToken ct)
    {
        var stamp = ReadPublishStamp();
        var changed = !_lastPublishStamp.HasValue || stamp > _lastPublishStamp.Value;
        _lastPublishStamp = stamp;
        IActionResult? purge = null;
        if (force || changed) purge = await Purge(new { purge_everything = true }, ct);
        return Ok(new { success = purge is not ObjectResult { StatusCode: >= 400 }, publishDetected = changed, purgeAttempted = force || changed, publishStamp = stamp });
    }

    [HttpPost("BroadcastDeployWarning")]
    public async Task<IActionResult> BroadcastDeployWarning([FromForm] int minutesUntilDeploy, [FromForm] int minutesToWait, CancellationToken ct)
    {
        if (minutesUntilDeploy is < 1 or > 240 || minutesToWait is < 1 or > 240) return BadRequest(new { success = false, message = "الدقائق لازم تكون بين 1 و 240." });
        var now = DateTimeOffset.UtcNow;
        var notice = new DeployWarningNotice(Guid.NewGuid().ToString("N"), $"سيتم رفع تحديث خلال {minutesUntilDeploy} دقيقة", "ستخسر عملك أثناء الرفع", $"انتظر {minutesToWait} دقيقة ثم عاود استعمال النظام", User.Identity?.Name ?? "مستخدم", now, now.AddMinutes(minutesUntilDeploy), now.AddMinutes(minutesUntilDeploy + minutesToWait));
        lock (WarningLock) _activeWarning = notice;
        await hub.Clients.All.SendAsync("DeployWarningBroadcast", notice, ct);
        return Ok(new { success = true, notice });
    }

    [HttpGet("ActiveDeployWarning")]
    [AllowAnonymous]
    public IActionResult ActiveDeployWarning() => Ok(new { success = true, warning = GetWarning() });

    [HttpPost("ClearDeployWarning")]
    public async Task<IActionResult> ClearDeployWarning(CancellationToken ct)
    {
        lock (WarningLock) _activeWarning = null;
        await hub.Clients.All.SendAsync("DeployWarningCleared", ct);
        return Ok(new { success = true });
    }

    [HttpGet("DeployPublishConfirmed")]
    [AllowAnonymous]
    public IActionResult DeployPublishConfirmed()
    {
        var warning = GetWarning();
        return Ok(new { success = true, confirmed = warning is not null && DateTimeOffset.UtcNow >= warning.DeployAt, buildStamp = ReadPublishStamp() });
    }

    private bool CloudflareEnabled => configuration.GetValue<bool>("Cloudflare:Enabled");

    private async Task<object> GetCloudflareStatus(CancellationToken ct)
    {
        var token = configuration["Cloudflare:ApiToken"];
        var zoneId = configuration["Cloudflare:ZoneId"];
        if (!CloudflareEnabled || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(zoneId)) return new { configured = false, valid = false, message = "Cloudflare integration is disabled or incomplete." };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.cloudflare.com/client/v4/user/tokens/verify");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClientFactory.CreateClient().SendAsync(request, ct);
            return new { configured = true, valid = response.IsSuccessStatusCode, status = (int)response.StatusCode, zoneId };
        }
        catch (Exception ex) { return new { configured = true, valid = false, message = ex.Message }; }
    }

    private async Task<IActionResult> Purge(object payload, CancellationToken ct)
    {
        var token = configuration["Cloudflare:ApiToken"];
        var zoneId = configuration["Cloudflare:ZoneId"];
        if (!CloudflareEnabled || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(zoneId)) return StatusCode(503, new { success = false, skipped = true, message = "Cloudflare integration is disabled or incomplete." });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.cloudflare.com/client/v4/zones/{Uri.EscapeDataString(zoneId)}/purge_cache");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(payload);
        using var response = await httpClientFactory.CreateClient().SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode ? Ok(new { success = true, body }) : StatusCode((int)response.StatusCode, new { success = false, body });
    }

    private DateTime ReadPublishStamp()
    {
        var root = environment.WebRootPath;
        if (!Directory.Exists(root)) return DateTime.MinValue;
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(file => { try { return System.IO.File.GetLastWriteTimeUtc(file); } catch { return DateTime.MinValue; } }).DefaultIfEmpty(DateTime.MinValue).Max();
    }

    private static DeployWarningNotice? GetWarning()
    {
        lock (WarningLock)
        {
            if (_activeWarning?.ExpiresAt > DateTimeOffset.UtcNow) return _activeWarning;
            _activeWarning = null; return null;
        }
    }
}

public sealed record DeployWarningNotice(string BroadcastId, string Line1, string Line2, string Line3, string SentBy, DateTimeOffset SentAt, DateTimeOffset DeployAt, DateTimeOffset ExpiresAt);
