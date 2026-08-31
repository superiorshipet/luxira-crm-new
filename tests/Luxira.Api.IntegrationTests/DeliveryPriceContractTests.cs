using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class DeliveryPriceContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task CityPriceWinsAndLegacyRouteMatchesCanonicalRoute()
    {
        using var client = CreateAuthenticatedClient();

        var canonical = await client.GetFromJsonAsync<PriceContract>(
            "/api/v1/delivery-companies/1/price?countryId=1&cityId=بغداد");
        var legacy = await client.GetFromJsonAsync<PriceContract>(
            "/DataList/GetDeliveryPrice?deliveryCompanyId=1&countryId=1&cityId=بغداد");

        Assert.Equal(new PriceContract(15.5m), canonical);
        Assert.Equal(canonical, legacy);
    }

    [Fact]
    public async Task MissingPricePreservesLegacyZeroResponse()
    {
        using var client = CreateAuthenticatedClient();

        var result = await client.GetFromJsonAsync<PriceContract>(
            "/api/v1/delivery-companies/999/price?countryId=1");

        Assert.Equal(new PriceContract(0m), result);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create("CallCenter"));
        return client;
    }

    private sealed record PriceContract(decimal Price);
}
