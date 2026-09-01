using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Luxira.Api.Features.Communication.Hubs;

[Authorize]
public class MessageHub : Hub
{
    public async Task JoinConversation(string conversationId)
    {
        if (string.IsNullOrEmpty(conversationId))
            throw new ArgumentException("Conversation ID cannot be null or empty", nameof(conversationId));

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
    }

    public async Task LeaveConversation(string conversationId)
    {
        if (!string.IsNullOrEmpty(conversationId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
        }
    }

    public async Task UpdateConversationList(string conversationId, string senderName, string latestMessage)
    {
        await Clients.All.SendAsync("UpdateConversationList", new
        {
            ConversationId = conversationId,
            SenderName = senderName,
            LatestMessage = latestMessage
        });
    }

    public async Task SendMessageToConversation(string conversationId, string senderName, string message)
    {
        await Clients.Group(conversationId).SendAsync("ReceiveMessage", new
        {
            ConversationId = conversationId,
            SenderName = senderName,
            Message = message,
            Timestamp = DateTime.UtcNow.ToString("HH:mm")
        });
    }

    public async Task UpdateCountryName(string conversationId, string countryName)
    {
        await Clients.All.SendAsync("UpdateCountryName", conversationId, countryName);
    }

    public async Task UpdateReadStatus(object data)
    {
        await Clients.All.SendAsync("UpdateReadStatus", data);
    }
}
