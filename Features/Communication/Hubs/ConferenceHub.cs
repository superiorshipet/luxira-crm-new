using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Luxira.Api.Features.Communication.Hubs;

[Authorize]
public class ConferenceHub : Hub
{
    private const int MaxRoomIdLength = 128;
    private const int MaxConnectionIdLength = 256;
    private const int MaxUserIdLength = 450;
    private const int MaxSignalingPayloadLength = 32_000;
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
        foreach (var room in RoomInviters.Where(entry => entry.Value == Context.ConnectionId).ToArray())
        {
            RoomInviters.TryRemove(room.Key, out _);
        }
        await base.OnDisconnectedAsync(exception);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task InviteUsers(string[] userIds, string roomId)
    {
        if (userIds == null || userIds.Length == 0 || userIds.Length > 100 ||
            string.IsNullOrWhiteSpace(roomId) || roomId.Length > 128)
            throw new HubException("Invalid conference invitation.");

        var adminId = Context.UserIdentifier;
        RoomInviters[roomId] = Context.ConnectionId;

        foreach (var userId in userIds)
        {
            if (string.IsNullOrEmpty(userId)) continue;
            await Clients.User(userId).SendAsync("IncomingCall", roomId, adminId);
        }
    }

    public async Task AcceptCall(string roomId)
    {
        ValidateRoomId(roomId);
        await Clients.OthersInGroup(roomId).SendAsync(
            "CallAccepted",
            Context.UserIdentifier,
            Context.ConnectionId);
    }

    public async Task DeclineCall(string roomId)
    {
        ValidateRoomId(roomId);
        var decliningUserId = Context.UserIdentifier;

        await Clients.OthersInGroup(roomId).SendAsync("CallDeclined", decliningUserId);

        if (RoomInviters.TryGetValue(roomId, out var inviterConnectionId) &&
            inviterConnectionId != Context.ConnectionId)
        {
            await Clients.Client(inviterConnectionId).SendAsync("CallDeclined", decliningUserId);
        }

        RoomInviters.TryRemove(roomId, out _);
    }

    public async Task EndCall(string roomId)
    {
        ValidateRoomId(roomId);
        await Clients.All.SendAsync("CallEnded", roomId);
        RoomInviters.TryRemove(roomId, out _);
    }

    public async Task JoinRoom(string roomId)
    {
        ValidateRoomId(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.OthersInGroup(roomId).SendAsync(
            "UserJoined",
            Context.UserIdentifier,
            Context.ConnectionId);
    }

    public async Task LeaveRoom(string roomId)
    {
        ValidateRoomId(roomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await Clients.OthersInGroup(roomId).SendAsync(
            "UserLeft",
            Context.UserIdentifier,
            Context.ConnectionId);
    }

    public Task SendOffer(string targetConnectionId, string offer) =>
        SendToConnection(targetConnectionId, "ReceiveOffer", offer);

    public Task SendAnswer(string targetConnectionId, string answer) =>
        SendToConnection(targetConnectionId, "ReceiveAnswer", answer);

    public Task SendIceCandidate(string targetConnectionId, string candidate) =>
        SendToConnection(targetConnectionId, "ReceiveIceCandidate", candidate);

    public async Task RequestScreenShare(string targetUserId)
    {
        var connectionId = FindConnectionId(targetUserId);
        if (connectionId is not null)
        {
            await Clients.Client(connectionId).SendAsync(
                "ScreenShareRequested",
                Context.ConnectionId);
        }
    }

    public Task RequestScreenShareByRoom(string roomId) =>
        SendToRoomExceptCaller(roomId, "ScreenShareRequested", null);

    public Task BroadcastScreenShareOffer(string roomId, string offer) =>
        SendToRoomExceptCaller(roomId, "ReceiveScreenShareOffer", offer);

    public Task SendStopScreenShare(string targetConnectionId) =>
        SendToConnection(targetConnectionId, "ReceiveStopScreenShare", null);

    public Task BroadcastScreenIceCandidate(string roomId, string candidate) =>
        SendToRoom(roomId, "ReceiveScreenIceCandidate", candidate);

    public Task SendScreenShareOffer(string targetConnectionId, string offer) =>
        SendToConnection(targetConnectionId, "ReceiveScreenShareOffer", offer);

    public Task SendScreenShareAnswer(string targetConnectionId, string answer) =>
        SendToConnection(targetConnectionId, "ReceiveScreenShareAnswer", answer);

    public Task SendScreenIceCandidate(string targetConnectionId, string candidate) =>
        SendToConnection(targetConnectionId, "ReceiveScreenIceCandidate", candidate);

    public async Task RequestCameraShare(string targetUserId)
    {
        var connectionId = FindConnectionId(targetUserId);
        if (connectionId is not null)
        {
            await Clients.Client(connectionId).SendAsync(
                "CameraShareRequested",
                Context.ConnectionId);
        }
    }

    public Task RequestCameraShareByRoom(string roomId) =>
        SendToRoomExceptCaller(roomId, "CameraShareRequested", null);

    public Task BroadcastCameraShareOffer(string roomId, string offer) =>
        SendToRoomExceptCaller(roomId, "ReceiveCameraShareOffer", offer);

    public Task SendStopCameraShare(string targetConnectionId) =>
        SendToConnection(targetConnectionId, "ReceiveStopCameraShare", null);

    public Task BroadcastCameraIceCandidate(string roomId, string candidate) =>
        SendToRoom(roomId, "ReceiveCameraIceCandidate", candidate);

    public Task SendCameraShareOffer(string targetConnectionId, string offer) =>
        SendToConnection(targetConnectionId, "ReceiveCameraShareOffer", offer);

    public Task SendCameraShareAnswer(string targetConnectionId, string answer) =>
        SendToConnection(targetConnectionId, "ReceiveCameraShareAnswer", answer);

    public Task SendCameraIceCandidate(string targetConnectionId, string candidate) =>
        SendToConnection(targetConnectionId, "ReceiveCameraIceCandidate", candidate);

    private string? FindConnectionId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > MaxUserIdLength)
            throw new HubException("Invalid user ID.");

        return ConnectedUsers.FirstOrDefault(entry => entry.Value == userId).Key;
    }

    private Task SendToConnection(
        string targetConnectionId,
        string eventName,
        string? payload)
    {
        ValidateConnectionId(targetConnectionId);
        ValidateSignalingPayload(payload);
        return payload is null
            ? Clients.Client(targetConnectionId).SendAsync(eventName, Context.ConnectionId)
            : Clients.Client(targetConnectionId).SendAsync(
                eventName,
                Context.ConnectionId,
                payload);
    }

    private Task SendToRoomExceptCaller(
        string roomId,
        string eventName,
        string? payload)
    {
        ValidateRoomId(roomId);
        ValidateSignalingPayload(payload);
        return payload is null
            ? Clients.GroupExcept(roomId, Context.ConnectionId)
                .SendAsync(eventName, Context.ConnectionId)
            : Clients.GroupExcept(roomId, Context.ConnectionId)
                .SendAsync(eventName, Context.ConnectionId, payload);
    }

    private Task SendToRoom(string roomId, string eventName, string payload)
    {
        ValidateRoomId(roomId);
        ValidateSignalingPayload(payload);
        return Clients.Group(roomId).SendAsync(
            eventName,
            Context.ConnectionId,
            payload);
    }

    private static void ValidateRoomId(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || roomId.Length > MaxRoomIdLength)
            throw new HubException("Invalid room ID.");
    }

    private static void ValidateConnectionId(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) ||
            connectionId.Length > MaxConnectionIdLength)
        {
            throw new HubException("Invalid connection ID.");
        }
    }

    private static void ValidateSignalingPayload(string? payload)
    {
        if (payload?.Length > MaxSignalingPayloadLength)
            throw new HubException("Signaling payload is too large.");
    }
}
