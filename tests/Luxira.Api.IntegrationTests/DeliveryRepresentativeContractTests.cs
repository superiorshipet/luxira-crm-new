using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class DeliveryRepresentativeContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task CanonicalAndLegacyRoutesPreserveCountryAndCityFilters()
    {
        using var client = CreateAuthenticatedClient();
        const string query = "?countryIds=1&cityIds=&cityIds=بغداد";

        var canonical = await client.GetFromJsonAsync<RepresentativeContract[]>(
            "/api/v1/delivery-representatives" + query);
        var legacy = await client.GetFromJsonAsync<RepresentativeContract[]>(
            "/DataList/GetAllDeliveryRepresentatives" + query);

        var expected = new[]
        {
            new RepresentativeContract(
                101,
                "Baghdad Representative",
                "/logos/baghdad.svg"),
        };
        Assert.Equal(expected, canonical);
        Assert.Equal(expected, legacy);
    }

    [Fact]
    public async Task BlankOnlyCityFilterMatchesLegacyNoFilterBehavior()
    {
        using var client = CreateAuthenticatedClient();

        var representatives = await client.GetFromJsonAsync<RepresentativeContract[]>(
            "/api/v1/delivery-representatives?countryIds=1&cityIds=%20");

        Assert.Equal(2, representatives!.Length);
        Assert.Equal("/static/DefaultImage.svg", representatives[1].LogoUrl);
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

    private sealed record RepresentativeContract(
        int Id,
        string Name,
        string LogoUrl);
}
