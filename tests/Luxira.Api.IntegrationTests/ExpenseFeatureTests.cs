using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Luxira.Api.Features.Expenses.DTOs;

namespace Luxira.Api.IntegrationTests;

public sealed class ExpenseFeatureTests(LuxiraApiFactory factory) : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateExpenseAndExchangeRateSucceeds()
    {
        var token = TestJwtTokenFactory.Create("Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var req = new CreateExpenseRequest(
            Description: "شراء قرطاسية للمكتب",
            Amount: 50000m,
            Country: 1,
            Category: "Office",
            Date: DateTime.UtcNow,
            AttachmentUrl: null,
            Notes: "فواتير شهرية"
        );

        var response = await _client.PostAsJsonAsync("/api/v1/expenses", req);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var expense = await response.Content.ReadFromJsonAsync<ExpenseDto>();
        Assert.NotNull(expense);
        Assert.Equal("شراء قرطاسية للمكتب", expense.Description);

        // Exchange Rate
        var rateReq = new UpdateExchangeRateRequest("USD", "IQD", 1500m);
        var rateRes = await _client.PostAsJsonAsync("/api/v1/expenses/exchange-rates", rateReq);
        Assert.Equal(HttpStatusCode.OK, rateRes.StatusCode);
    }
}
