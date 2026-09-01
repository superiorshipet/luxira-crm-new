using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Luxira.Api.Features.Orders.DTOs;

namespace Luxira.Api.IntegrationTests;

public sealed class OrderFeatureTests(LuxiraApiFactory factory) : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateOrderAndStatusUpdateFlowSucceeds()
    {
        var token = TestJwtTokenFactory.Create("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createReq = new CreateOrderRequest(
            Country: 1,
            State: "بغداد",
            OrderSource: 1,
            SourceName: "فيسبوك",
            ManufacturingCompanyId: 1,
            DeliveryCompanyId: 1,
            TelephoneNumber: "07712345678",
            SecondTelephoneNumber: "07787654321",
            CustomerName: "علي الكرخي",
            Notes: "توصيل سريع",
            Address: "المنصور - شارع 14 رمضان",
            TotalPrice: 35000m,
            DeliveryPrice: 5000m,
            CustomerDeliveryPrice: 5000m,
            ChatUrl: "https://m.me/test",
            Items: new List<CreateOrderItemRequest>
            {
                new(WarehouseId: 1, Quantity: 2, Price: 15000m, Cost: 10000m)
            }
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/orders", createReq);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(createdOrder);
        Assert.True(createdOrder.Id > 0);
        Assert.Equal("علي الكرخي", createdOrder.CustomerName);
        Assert.Equal(1, createdOrder.OrderStatus);

        // Update status to 2 (مؤكد)
        var statusReq = new UpdateOrderStatusRequest(2, "Confirmed by phone", "Call center agent note");
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/orders/{createdOrder.Id}/status", statusReq);
        var updateContent = await updateResponse.Content.ReadAsStringAsync();
        Assert.True(updateResponse.IsSuccessStatusCode, $"Update status failed: {updateResponse.StatusCode} - {updateContent}");

        var updatedOrder = await updateResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(updatedOrder);
        Assert.Equal(2, updatedOrder.OrderStatus);

        // Get Orders List
        var listResponse = await _client.GetFromJsonAsync<OrderListResult>($"/api/v1/orders?search={createdOrder.Id}");
        Assert.NotNull(listResponse);
        Assert.True(listResponse.TotalCount >= 1);

        // Get Stats
        var statsResponse = await _client.GetFromJsonAsync<OrderStatsDto>("/api/v1/orders/stats");
        Assert.NotNull(statsResponse);
        Assert.True(statsResponse.TotalOrders >= 1);
    }
}
