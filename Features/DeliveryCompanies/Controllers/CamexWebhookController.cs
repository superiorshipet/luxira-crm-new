using System.Security.Cryptography;
using System.Text;
using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.Orders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController, AllowAnonymous]
[Route("api/v1/webhooks/camex"), Route("api/camex/webhook"), Route("CamexWebhook")]
public sealed class CamexWebhookController(ApplicationDbContext context, OrderService orders, IConfiguration configuration, ILogger<CamexWebhookController> logger) : ControllerBase
{
    [HttpPost, HttpPost("ProcessWebhook")]
    public async Task<IActionResult> ProcessWebhook([FromBody] CamexWebhookPayload payload, CancellationToken ct)
    {
        var bodySecret = configuration["Camex:WebhookSecretKey"];
        if (string.IsNullOrWhiteSpace(bodySecret) || !FixedTimeEquals(payload.SecretKey, bodySecret)) return Unauthorized();
        var tracking = payload.Id ?? (long.TryParse(payload.TrackingNumber, out var parsed) ? parsed : null);
        if (!tracking.HasValue || tracking <= 0) return Envelope("missing id");
        var state = payload.State ?? LegacyTextState(payload.Status);
        if (!state.HasValue) return Envelope("missing or unknown state");
        var order = await context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.CamexTrackingNumber == tracking, ct);
        if (order is null) return Envelope("unknown tracking number");
        var mapping = CamexReconciliationService.Map(state.Value); var target = mapping.Status; var now = DateTime.UtcNow;
        var advisory = mapping.Advisory;
        await context.Orders.Where(item => item.Id == order.Id).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.CamexState, state).SetProperty(item => item.CamexStateChangedAt, now)
            .SetProperty(item => item.CamexAdvisoryState, advisory == null ? null : state)
            .SetProperty(item => item.CamexAdvisoryAt, advisory == null ? null : now)
            .SetProperty(item => item.CamexAdvisoryNote, advisory), ct);
        if (target.HasValue && target.Value != order.OrderStatus)
            await orders.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest(target.Value, $"Camex state {state.Value}", payload.Notes), OrderStatusActor.TrustedSystem("camex-webhook"), ct);
        else if (!target.HasValue && state is not 12) logger.LogWarning("Unmapped CAMEX state {State} for order {OrderId}", state, order.Id);
        return Envelope(target.HasValue ? "applied" : "recorded without status change");
    }
    private OkObjectResult Envelope(string trace) => Ok(new { type = 1, messages = Array.Empty<string>(), traceId = trace });
    private static int? LegacyTextState(string? status) => status?.Trim().ToLowerInvariant() switch { "delivered" => 6, "returned" => 11, "in_transit" => 5, "cancelled" => 16, _ => null };
    private static bool FixedTimeEquals(string? supplied, string expected) { if (string.IsNullOrEmpty(supplied)) return false; var left = Encoding.UTF8.GetBytes(supplied); var right = Encoding.UTF8.GetBytes(expected); return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right); }
}
public sealed record CamexWebhookPayload(long? Id, int? State, string? SecretKey, string? TrackingNumber = null, string? Status = null, string? Notes = null, DateTime? Timestamp = null, string? EventId = null);
