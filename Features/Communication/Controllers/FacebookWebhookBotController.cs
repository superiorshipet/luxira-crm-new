using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Luxira.Api.Features.Communication.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Route("api/v1/webhooks/facebook")]
[Route("api/facebook/webhook")]
[Route("FacebookWebhookBot")]
public sealed class FacebookWebhookBotController(IConfiguration configuration, IHttpClientFactory clients, IHubContext<MessageHub> hub) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, FacebookConversation> ConversationsState = new(StringComparer.Ordinal);

    [HttpGet]
    [HttpGet("Webhook")]
    [HttpGet("/FacebookWebhookBot/VerifyWebhook")]
    [AllowAnonymous]
    public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? token, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = configuration["Facebook:VerifyToken"];
        if (!string.IsNullOrWhiteSpace(expected) && mode == "subscribe" && CryptographicEquals(token, expected)) return Content(challenge ?? string.Empty, "text/plain");
        return BadRequest("Invalid verification token or mode.");
    }

    [HttpPost]
    [HttpPost("Webhook")]
    [HttpPost("/FacebookWebhookBot/ReceiveWebhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ReceiveWebhook([FromBody] JsonElement payload, CancellationToken ct)
    {
        if (!payload.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array) return Ok(new { status = "EVENT_RECEIVED" });
        foreach (var entry in entries.EnumerateArray())
        {
            var pageId = String(entry, "id") ?? string.Empty;
            if (!entry.TryGetProperty("messaging", out var messages) || messages.ValueKind != JsonValueKind.Array) continue;
            foreach (var eventItem in messages.EnumerateArray())
            {
                var senderId = eventItem.TryGetProperty("sender", out var sender) ? String(sender, "id") : null; if (string.IsNullOrWhiteSpace(senderId)) continue;
                var conversation = ConversationsState.GetOrAdd(senderId, id => new FacebookConversation(id, pageId)); conversation.PageId = pageId; conversation.UpdatedAt = DateTimeOffset.UtcNow;
                if (eventItem.TryGetProperty("message", out var message)) { var text = String(message, "text") ?? ""; conversation.Messages.Enqueue(new FacebookMessage(Guid.NewGuid().ToString("N"), text, true, DateTimeOffset.UtcNow)); await hub.Clients.All.SendAsync("UpdateConversationList", new { conversationId = senderId, senderName = conversation.SenderName ?? senderId, latestMessage = text }, ct); await hub.Clients.Group(senderId).SendAsync("ReceiveMessage", new { conversationId = senderId, senderName = conversation.SenderName ?? senderId, message = text }, ct); }
            }
        }
        return Ok(new { status = "EVENT_RECEIVED" });
    }

    [Authorize]
    [HttpGet("Conversations")]
    [HttpGet("/FacebookWebhookBot/Conversations")]
    [HttpPost("/FacebookWebhookBot/Conversations")]
    public Task<IActionResult> Conversations(CancellationToken ct) => GetAllConversations(null, null, null, null, ct);

    [Authorize]
    [HttpGet("GetAllConversations")]
    [HttpGet("allconversations")]
    [HttpGet("/FacebookWebhookBot/GetAllConversations")]
    public async Task<IActionResult> GetAllConversations(string? searchTerm, bool? isRead, bool? isConvertedToOrder, string? sortOrder, CancellationToken ct)
    {
        var query = ConversationsState.Values.AsEnumerable(); if (!string.IsNullOrWhiteSpace(searchTerm)) query = query.Where(item => (item.SenderName ?? item.Id).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)); if (isRead.HasValue) query = query.Where(item => item.IsRead == isRead); if (isConvertedToOrder.HasValue) query = query.Where(item => item.IsConvertedToOrder == isConvertedToOrder); query = sortOrder == "asc" ? query.OrderBy(item => item.UpdatedAt) : query.OrderByDescending(item => item.UpdatedAt);
        foreach (var item in query.Where(item => item.SenderName is null).Take(20)) await HydrateProfile(item, ct);
        return Ok(new { conversations = query.Select(item => new { conversationId = item.Id, senderName = item.SenderName ?? item.Id, item.PageId, item.IsRead, item.IsConvertedToOrder, item.CountryId, item.ProductName, item.UpdatedAt, latestMessage = item.Messages.LastOrDefault()?.Text }) });
    }

    [Authorize]
    [HttpPost("ToggleOrderStatus")]
    [HttpPost("toggle-order/{conversationId}")]
    [HttpPost("/FacebookWebhookBot/ToggleOrderStatus")]
    public IActionResult ToggleOrderStatus([FromRoute] string? conversationId, [FromForm(Name = "conversationId")] string? formConversationId = null) { conversationId ??= formConversationId; if (string.IsNullOrWhiteSpace(conversationId) || !ConversationsState.TryGetValue(conversationId, out var item)) return NotFound(); item.IsConvertedToOrder = !item.IsConvertedToOrder; return Ok(new { success = true, item.IsConvertedToOrder }); }

    [Authorize]
    [HttpPost("ToggleReadStatus")]
    [HttpPost("toggle-read/{conversationId}")]
    [HttpPost("/FacebookWebhookBot/ToggleReadStatus")]
    public IActionResult ToggleReadStatus([FromRoute] string? conversationId, [FromForm(Name = "conversationId")] string? formConversationId = null) { conversationId ??= formConversationId; if (string.IsNullOrWhiteSpace(conversationId) || !ConversationsState.TryGetValue(conversationId, out var item)) return NotFound(); item.IsRead = !item.IsRead; return Ok(new { success = true, item.IsRead }); }

    [Authorize]
    [HttpPost("MarkConversationAsRead")]
    [HttpPost("conversations/{id}/markAsRead")]
    [HttpPost("/FacebookWebhookBot/MarkConversationAsRead")]
    public IActionResult MarkConversationAsRead([FromRoute] string? id, [FromForm(Name = "id")] string? formId = null) { id ??= formId; if (string.IsNullOrWhiteSpace(id) || !ConversationsState.TryGetValue(id, out var item)) return NotFound(); item.IsRead = true; return Ok(new { success = true }); }

    [Authorize]
    [HttpGet("conversations/{id}/messages")]
    [HttpGet("GetMessages")]
    [HttpGet("/FacebookWebhookBot/GetMessages")]
    public IActionResult GetMessages(string id) => ConversationsState.TryGetValue(id, out var item) ? Ok(new { messages = item.Messages.ToArray() }) : NotFound();

    [Authorize]
    [HttpPost("conversations/{id}/messages")]
    [HttpPost("SendMessage")]
    [HttpPost("/FacebookWebhookBot/SendMessage")]
    public async Task<IActionResult> SendMessage(string id, [FromBody] FacebookSendRequest request, CancellationToken ct)
    {
        if (!ConversationsState.TryGetValue(id, out var conversation)) return NotFound(); var text = request.Text?.Trim(); if (string.IsNullOrWhiteSpace(text) || text.Length > 2000) return BadRequest(); var result = await SendGraphMessage(conversation.PageId, id, text, ct); if (!result.success) return StatusCode(result.status, new { success = false, message = result.error }); var message = new FacebookMessage(result.messageId ?? Guid.NewGuid().ToString("N"), text, false, DateTimeOffset.UtcNow); conversation.Messages.Enqueue(message); conversation.UpdatedAt = message.CreatedAt; await hub.Clients.Group(id).SendAsync("ReceiveMessage", new { conversationId = id, senderName = User.Identity?.Name ?? "Luxira", message = text }, ct); return Ok(new { success = true, message });
    }

    [Authorize]
    [HttpPost("UpdateCountry")]
    [HttpPost("/FacebookWebhookBot/UpdateCountry")]
    public IActionResult UpdateCountry([FromForm] string conversationId, [FromForm] int countryId) { if (!ConversationsState.TryGetValue(conversationId, out var item)) return NotFound(); item.CountryId = countryId; return Ok(new { success = true }); }

    [Authorize]
    [HttpPost("UpdateProduct")]
    [HttpPost("/FacebookWebhookBot/UpdateProduct")]
    public IActionResult UpdateProduct([FromForm] string conversationId, [FromForm] string productName) { if (!ConversationsState.TryGetValue(conversationId, out var item)) return NotFound(); item.ProductName = productName?.Trim(); return Ok(new { success = true }); }

    [Authorize]
    [HttpPost("SendOfferMessages")]
    [HttpPost("send-offer-messages/{pageId}")]
    [HttpPost("/FacebookWebhookBot/SendOfferMessages")]
    public async Task<IActionResult> SendOfferMessages([FromRoute] string? pageId, [FromForm(Name = "pageId")] string? formPageId, CancellationToken ct)
    {
        pageId ??= formPageId;
        if (string.IsNullOrWhiteSpace(pageId)) return BadRequest(new { success = false, message = "Page ID is required." });
        var messages = configuration.GetSection($"Facebook:Pages:{pageId}:OfferMessages").Get<string[]>() ?? []; var recipients = ConversationsState.Values.Where(item => item.PageId == pageId && !item.IsConvertedToOrder).ToList(); var sent = 0; var failed = 0;
        foreach (var recipient in recipients) foreach (var message in messages) { var result = await SendGraphMessage(pageId, recipient.Id, message, ct); if (result.success) { sent++; recipient.Messages.Enqueue(new FacebookMessage(result.messageId ?? Guid.NewGuid().ToString("N"), message, false, DateTimeOffset.UtcNow)); } else failed++; }
        return Ok(new { success = failed == 0, recipients = recipients.Count, sent, failed });
    }

    private async Task<(bool success, int status, string? messageId, string? error)> SendGraphMessage(string pageId, string recipientId, string text, CancellationToken ct)
    {
        var token = configuration[$"Facebook:Pages:{pageId}:AccessToken"]; if (string.IsNullOrWhiteSpace(token)) return (false, 503, null, "Facebook page access token is not configured."); var version = configuration["Facebook:GraphApiVersion"] ?? "v23.0"; using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{version}/me/messages?access_token={Uri.EscapeDataString(token)}") { Content = JsonContent.Create(new { recipient = new { id = recipientId }, message = new { text } }) }; try { using var response = await clients.CreateClient().SendAsync(request, ct); var body = await response.Content.ReadAsStringAsync(ct); string? messageId = null; try { using var json = JsonDocument.Parse(body); messageId = String(json.RootElement, "message_id"); } catch (JsonException) { } return response.IsSuccessStatusCode ? (true, (int)response.StatusCode, messageId, null) : (false, (int)response.StatusCode, null, body); } catch (Exception ex) { return (false, 502, null, ex.Message); }
    }

    private async Task HydrateProfile(FacebookConversation item, CancellationToken ct) { var token = configuration[$"Facebook:Pages:{item.PageId}:AccessToken"]; if (string.IsNullOrWhiteSpace(token)) return; var version = configuration["Facebook:GraphApiVersion"] ?? "v23.0"; try { using var response = await clients.CreateClient().GetAsync($"https://graph.facebook.com/{version}/{Uri.EscapeDataString(item.Id)}?fields=name&access_token={Uri.EscapeDataString(token)}", ct); if (!response.IsSuccessStatusCode) return; using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); item.SenderName = String(json.RootElement, "name"); } catch { } }
    private static string? String(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool CryptographicEquals(string? left, string right) { var a = System.Text.Encoding.UTF8.GetBytes(left ?? ""); var b = System.Text.Encoding.UTF8.GetBytes(right); return a.Length == b.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b); }
}

public sealed class FacebookConversation(string id, string pageId)
{
    public string Id { get; } = id; public string PageId { get; set; } = pageId; public string? SenderName { get; set; } public bool IsRead { get; set; } public bool IsConvertedToOrder { get; set; } public int? CountryId { get; set; } public string? ProductName { get; set; } public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; public ConcurrentQueue<FacebookMessage> Messages { get; } = new();
}
public sealed record FacebookMessage(string Id, string Text, bool IsFromUser, DateTimeOffset CreatedAt);
public sealed record FacebookSendRequest(string? Text);
