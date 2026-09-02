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
        await Groups.AddToGroupAsync(Context.ConnectionId, ValidateConversationId(conversationId));
    }

    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ValidateConversationId(conversationId));
    }

    public async Task UpdateConversationList(string conversationId, string senderName, string latestMessage)
    {
        ValidateMessage(latestMessage);
        ValidateConversationId(conversationId);
        ValidateMessage(latestMessage);
        await Clients.All.SendAsync("UpdateConversationList", new
        {
            ConversationId = conversationId,
            SenderName = senderName,
            LatestMessage = latestMessage
        });
    }

    public async Task SendMessageToConversation(string conversationId, string senderName, string message)
    {
        ValidateMessage(message);
        await Clients.Group(ValidateConversationId(conversationId)).SendAsync("ReceiveMessage", new
        {
            ConversationId = conversationId,
            SenderName = senderName,
            Message = message,
            Timestamp = DateTime.UtcNow.ToString("HH:mm")
        });
    }

    public async Task UpdateCountryName(string conversationId, string countryName)
    {
        if (countryName?.Length > 100) throw new HubException("Country name is too long.");
        ValidateConversationId(conversationId);
        await Clients.All
            .SendAsync("UpdateCountryName", conversationId, countryName);
    }

    public async Task UpdateReadStatus(object data)
    {
        await Clients.All.SendAsync("UpdateReadStatus", data);
    }

    private static string ValidateConversationId(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || conversationId.Length > MaxConversationIdLength)
            throw new HubException("Invalid conversation ID.");
        return conversationId.Trim();
    }

    private static void ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength)
            throw new HubException("Invalid message.");
    }
}
