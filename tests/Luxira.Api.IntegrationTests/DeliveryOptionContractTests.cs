using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class DeliveryOptionContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task CallCenterOrderUsesOnlyItsAssignedDeliveryCompany()
    {
        using var client = CreateAuthenticatedClient("CallCenter");

        var options = await client.GetFromJsonAsync<OptionContract[]>(
            "/api/v1/delivery-options?countryId=1&orderId=500");

        Assert.Equal(
            new OptionContract(1, "Iraq Express", "/logos/iraq-express.svg", false),
            Assert.Single(options!));
    }

    [Fact]
    public async Task MissingCallCenterAssignmentReturnsLegacyEmptyList()
    {
        using var client = CreateAuthenticatedClient("CallCenter");

        var options = await client.GetFromJsonAsync<OptionContract[]>(
            "/DataList/GetAllDeliveryCompaniesAndRepresentatives?orderId=501");

        Assert.Empty(options!);
    }

    [Fact]
    public async Task NonCallCenterIgnoresOrderIdAndAppliesRepresentativeCityOnly()
    {
        using var client = CreateAuthenticatedClient("Admin");

        var options = await client.GetFromJsonAsync<OptionContract[]>(
            "/api/v1/delivery-options?countryId=1&cityId=بغداد&orderId=501");

        Assert.Equal(2, options!.Length);
        Assert.False(options[0].IsRepresentative);
        Assert.True(options[1].IsRepresentative);
        Assert.Equal(101, options[1].Id);
    }

    private HttpClient CreateAuthenticatedClient(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create(role));
        return client;
    }

    private sealed record OptionContract(
        int Id,
        string Name,
        string LogoUrl,
        bool IsRepresentative);
}
