using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Luxira.Api.Features.Communication.Hubs;

[Authorize]
public class ConferenceHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new();
    private static readonly ConcurrentDictionary<string, string> RoomInviters = new();

    public static bool IsUserConnected(string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && ConnectedUsers.Values.Contains(userId);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            ConnectedUsers.TryAdd(Context.ConnectionId, userId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectedUsers.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task InviteUsers(string[] userIds, string roomId)
    {
        if (userIds == null || string.IsNullOrWhiteSpace(roomId)) return;

        var adminId = Context.UserIdentifier;
        RoomInviters[roomId] = Context.ConnectionId;

        foreach (var userId in userIds)
        {
            if (string.IsNullOrEmpty(userId)) continue;
            await Clients.User(userId).SendAsync("IncomingCall", roomId, adminId);
        }
    }

    public async Task DeclineCall(string roomId, string reason)
    {
        if (RoomInviters.TryGetValue(roomId, out var inviterConnectionId))
        {
            await Clients.Client(inviterConnectionId).SendAsync("CallDeclined", Context.UserIdentifier, reason);
        }
    }
}
