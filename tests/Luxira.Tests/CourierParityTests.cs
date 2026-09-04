using System.Text.Json;
using Luxira.Api.Features.DeliveryCompanies.Controllers;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Luxira.Api.Features.Orders.Models;

namespace Luxira.Tests;

public sealed class CourierParityTests
{
    [Theory]
    [InlineData("Requested", OrderStatusCodes.New)]
    [InlineData("ForPacking", OrderStatusCodes.Prepared)]
    [InlineData("OnDelivery", OrderStatusCodes.InDelivery)]
    [InlineData("Complete", OrderStatusCodes.Delivered)]
    [InlineData("SemiFailed", OrderStatusCodes.FailedDelivery)]
    public void Sandoog_statuses_match_legacy(string eventType, int expected)
    {
        Assert.True(SandoogWebhookController.TryMapStatus(eventType, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, OrderStatusCodes.Prepared)]
    [InlineData(18, OrderStatusCodes.InDelivery)]
    [InlineData(6, OrderStatusCodes.Delivered)]
    [InlineData(11, OrderStatusCodes.FailedDelivery)]
    [InlineData(20, OrderStatusCodes.Suspicious)]
    public void Camex_states_match_legacy(int state, int expected) => Assert.Equal(expected, CamexReconciliationService.Map(state).Status);

    [Fact]
    public void Sandoog_provider_payload_binds_exact_wire_names()
    {
        var payload = JsonSerializer.Deserialize<SandoogWebhookPayload>("""{"event_id":"evt_1","event_type":"Returned","external_reference":"42","event_data":{"id":"ord_1","fulfillment_id":"ful_1"}}""");
        Assert.NotNull(payload); Assert.Equal("Returned", payload.EventType); Assert.Equal("42", payload.ExternalReference); Assert.Equal("ord_1", payload.EventData?.Id); Assert.Equal("ful_1", payload.EventData?.FulfillmentId);
    }
}
