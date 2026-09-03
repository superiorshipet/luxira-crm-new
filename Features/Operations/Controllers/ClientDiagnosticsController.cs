using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Operations.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
public sealed class ClientDiagnosticsController(ApplicationDbContext context) : ControllerBase
{
    [HttpPost("/api/client-console/batch")]
    [Authorize]
    public async Task<IActionResult> Batch([FromBody] JsonElement payload, CancellationToken ct)
    {
        context.AppLogs.Add(new AppLog { CreatedAtUtc = DateTime.UtcNow, Level = "Information", Category = "ClientConsole", Message = Limit(payload.GetRawText(), 20_000), Type = "Client" });
        await context.SaveChangesAsync(ct);
        return Accepted(new { success = true });
    }

    [HttpPost("/api/create-order-diagnostics/stall")]
    [Authorize]
    public Task<IActionResult> Stall([FromBody] JsonElement payload, CancellationToken ct) => RecordMetric("CreateOrderStall", payload, ct);

    [HttpPost("/api/create-order-diagnostics/field-wait")]
    [Authorize]
    public Task<IActionResult> FieldWait([FromBody] JsonElement payload, CancellationToken ct) => RecordMetric("CreateOrderFieldWait", payload, ct);

    private async Task<IActionResult> RecordMetric(string kind, JsonElement payload, CancellationToken ct)
    {
        var duration = payload.TryGetProperty("durationMs", out var value) && value.TryGetDouble(out var milliseconds) ? Math.Max(0, milliseconds) : 0;
        context.AppMetrics.Add(new AppMetric { CreatedAtUtc = DateTime.UtcNow, Kind = kind, DurationMs = duration, MetricsJson = Limit(payload.GetRawText(), 20_000), UserName = User.Identity?.Name });
        await context.SaveChangesAsync(ct);
        return Accepted(new { success = true });
    }

    private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];
}
