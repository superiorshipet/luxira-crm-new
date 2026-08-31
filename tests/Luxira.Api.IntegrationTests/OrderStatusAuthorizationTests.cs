using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class OrderStatusAuthorizationTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task AdministratorReceivesFullDeclarationOrderedCatalog()
    {
        var statuses = await GetStatuses("Admin");

        Assert.Equal(32, statuses.Length);
        Assert.Equal(new OrderStatusContract(0, "طلب_جديد", "/static/blue.svg"), statuses[0]);
        Assert.Equal(
            new OrderStatusContract(32, "حالة_مشكوك_بها", "/static/yellow.svg"),
            statuses[^1]);
        Assert.Equal(30, statuses[7].Id);
        Assert.Equal(19, statuses[8].Id);
    }

    [Fact]
    public async Task DeliveryRoleReceivesOnlyLegacyFiveStatusSubset()
    {
        var statuses = await GetStatuses("DeliveryCompany");

        Assert.Equal([0, 2, 4, 6, 7], statuses.Select(status => status.Id));
    }

    [Fact]
    public async Task CallCenterAndFollowUpKeepTheirDifferentLegacyCatalogs()
    {
        var callCenter = await GetStatuses("CallCenter");
        var followUp = await GetStatuses("FollowUpDepartment");

        Assert.Equal(20, callCenter.Length);
        Assert.Equal("أخطاء_التوصيل", callCenter.Single(status => status.Id == 10).Name);
        Assert.DoesNotContain(callCenter, status => status.Id == 0);
        Assert.Contains(followUp, status => status.Id == 0);
        Assert.Contains(followUp, status => status.Id == 31);
    }

    [Fact]
    public async Task UnlistedAuthenticatedRoleIsForbidden()
    {
        using var client = CreateAuthenticatedClient("Observer");
        using var response = await client.GetAsync(
            "/api/v1/reference-data/order-statuses");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<OrderStatusContract[]> GetStatuses(string role)
    {
        using var client = CreateAuthenticatedClient(role);
        var canonical = await client.GetFromJsonAsync<OrderStatusContract[]>(
            "/api/v1/reference-data/order-statuses");
        var legacy = await client.GetFromJsonAsync<OrderStatusContract[]>(
            "/DataList/GetAllOrderStatuses");

        Assert.NotNull(canonical);
        Assert.Equal(canonical, legacy);
        return canonical;
    }

    private HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create(roles));
        return client;
    }

    private sealed record OrderStatusContract(
        int Id,
        string Name,
        string ImageUrl);
}
