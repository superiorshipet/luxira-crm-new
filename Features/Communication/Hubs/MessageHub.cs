using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Luxira.Api.Features.Communication.Hubs;

[Authorize]
public class MessageHub : Hub
{
    private const int MaxConversationIdLength = 128;
    private const int MaxMessageLength = 10_000;

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
    }

    public async Task UpdateConversationList(string conversationId, string senderName, string latestMessage)
    {
        ValidateMessage(latestMessage);
        await Clients.Group(GetConversationGroup(conversationId)).SendAsync("UpdateConversationList", new
        {
            ConversationId = conversationId,
            SenderName = GetSenderName(),
            LatestMessage = latestMessage
        });
    }

    public async Task SendMessageToConversation(string conversationId, string senderName, string message)
    {
        ValidateMessage(message);
        await Clients.Group(GetConversationGroup(conversationId)).SendAsync("ReceiveMessage", new
        {
            ConversationId = conversationId,
            SenderName = GetSenderName(),
            Message = message,
            Timestamp = DateTime.UtcNow.ToString("HH:mm")
        });
    }

    public async Task UpdateCountryName(string conversationId, string countryName)
    {
        if (countryName?.Length > 100) throw new HubException("Country name is too long.");
        await Clients.Group(GetConversationGroup(conversationId))
            .SendAsync("UpdateCountryName", conversationId, countryName);
    }

    public async Task UpdateReadStatus(string conversationId, object data)
    {
        await Clients.Group(GetConversationGroup(conversationId)).SendAsync("UpdateReadStatus", data);
    }

    private string GetSenderName() =>
        Context.User?.Identity?.Name ?? Context.UserIdentifier ?? "Unknown user";

    private static string GetConversationGroup(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || conversationId.Length > MaxConversationIdLength)
            throw new HubException("Invalid conversation ID.");
        return $"conversation_{conversationId.Trim()}";
    }

    private static void ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength)
            throw new HubException("Invalid message.");
    }
}
