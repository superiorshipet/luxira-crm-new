using Luxira.Api.Features.Communication.Hubs;
using Luxira.Api.Features.ManufacturingCompanies.Hubs;
using Luxira.Api.Features.Orders.Hubs;

namespace Luxira.Tests;

public sealed class RealtimeContractTests
{
    [Theory]
    [InlineData(nameof(ConferenceHub.InviteUsers), 2)]
    [InlineData(nameof(ConferenceHub.AcceptCall), 1)]
    [InlineData(nameof(ConferenceHub.DeclineCall), 1)]
    [InlineData(nameof(ConferenceHub.EndCall), 1)]
    [InlineData(nameof(ConferenceHub.JoinRoom), 1)]
    [InlineData(nameof(ConferenceHub.LeaveRoom), 1)]
    [InlineData(nameof(ConferenceHub.SendOffer), 2)]
    [InlineData(nameof(ConferenceHub.SendAnswer), 2)]
    [InlineData(nameof(ConferenceHub.SendIceCandidate), 2)]
    [InlineData(nameof(ConferenceHub.RequestScreenShare), 1)]
    [InlineData(nameof(ConferenceHub.RequestScreenShareByRoom), 1)]
    [InlineData(nameof(ConferenceHub.BroadcastScreenShareOffer), 2)]
    [InlineData(nameof(ConferenceHub.SendStopScreenShare), 1)]
    [InlineData(nameof(ConferenceHub.BroadcastScreenIceCandidate), 2)]
    [InlineData(nameof(ConferenceHub.SendScreenShareOffer), 2)]
    [InlineData(nameof(ConferenceHub.SendScreenShareAnswer), 2)]
    [InlineData(nameof(ConferenceHub.SendScreenIceCandidate), 2)]
    [InlineData(nameof(ConferenceHub.RequestCameraShare), 1)]
    [InlineData(nameof(ConferenceHub.RequestCameraShareByRoom), 1)]
    [InlineData(nameof(ConferenceHub.BroadcastCameraShareOffer), 2)]
    [InlineData(nameof(ConferenceHub.SendStopCameraShare), 1)]
    [InlineData(nameof(ConferenceHub.BroadcastCameraIceCandidate), 2)]
    [InlineData(nameof(ConferenceHub.SendCameraShareOffer), 2)]
    [InlineData(nameof(ConferenceHub.SendCameraShareAnswer), 2)]
    [InlineData(nameof(ConferenceHub.SendCameraIceCandidate), 2)]
    public void ConferenceHub_PreservesLegacyMethodSignatures(
        string methodName,
        int parameterCount)
    {
        AssertHubMethod(typeof(ConferenceHub), methodName, parameterCount);
    }

    [Theory]
    [InlineData(nameof(MessageHub.JoinConversation), 1)]
    [InlineData(nameof(MessageHub.LeaveConversation), 1)]
    [InlineData(nameof(MessageHub.UpdateConversationList), 3)]
    [InlineData(nameof(MessageHub.SendMessageToConversation), 3)]
    [InlineData(nameof(MessageHub.UpdateCountryName), 2)]
    [InlineData(nameof(MessageHub.UpdateReadStatus), 1)]
    public void MessageHub_PreservesLegacyMethodSignatures(
        string methodName,
        int parameterCount)
    {
        AssertHubMethod(typeof(MessageHub), methodName, parameterCount);
    }

    [Theory]
    [InlineData(nameof(OrderHub.NotifyClientsWithFailedOrderStatusSound), 0)]
    [InlineData(nameof(OrderHub.NotifyWithDeliverdOrderStatusNotification), 2)]
    [InlineData(nameof(OrderHub.NotifyWithFailedOrderStatusNotification), 2)]
    [InlineData(nameof(OrderHub.NotifyWithFixedOrderStatusNotification), 2)]
    [InlineData(nameof(OrderHub.NotifyNewOrderPost), 2)]
    public void OrderHub_PreservesLegacyNotificationMethods(
        string methodName,
        int parameterCount)
    {
        AssertHubMethod(typeof(OrderHub), methodName, parameterCount);
    }

    [Theory]
    [InlineData(nameof(StoreCodeEditorHub.JoinFile), 1)]
    [InlineData(nameof(StoreCodeEditorHub.RequestTyping), 1)]
    [InlineData(nameof(StoreCodeEditorHub.KeepTyping), 1)]
    [InlineData(nameof(StoreCodeEditorHub.SendChange), 2)]
    [InlineData(nameof(StoreCodeEditorHub.StopTyping), 1)]
    public void StoreCodeEditorHub_PreservesLegacyEditingMethods(
        string methodName,
        int parameterCount)
    {
        AssertHubMethod(typeof(StoreCodeEditorHub), methodName, parameterCount);
    }

    private static void AssertHubMethod(
        Type hubType,
        string methodName,
        int parameterCount)
    {
        var method = hubType.GetMethods()
            .SingleOrDefault(candidate =>
                candidate.Name == methodName &&
                candidate.GetParameters().Length == parameterCount);

        Assert.NotNull(method);
    }
}
