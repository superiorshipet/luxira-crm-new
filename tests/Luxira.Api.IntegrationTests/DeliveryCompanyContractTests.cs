using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class DeliveryCompanyContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task CanonicalAndLegacyRoutesPreserveFilteringAndMediaUrls()
    {
        using var client = CreateAuthenticatedClient();

        var canonical = await client.GetFromJsonAsync<DeliveryCompanyContract[]>(
            "/api/v1/delivery-companies?countryIds=1");
        var legacy = await client.GetFromJsonAsync<DeliveryCompanyContract[]>(
            "/DataList/GetAllDeliveryCompanies?countryIds=1");

        var expected = new[]
        {
            new DeliveryCompanyContract(
                1,
                "Iraq Express",
                "/logos/iraq-express.svg"),
        };
        Assert.Equal(expected, canonical);
        Assert.Equal(expected, legacy);
    }

    [Fact]
    public async Task MissingLogoUsesLegacyFallback()
    {
        using var client = CreateAuthenticatedClient();

        var companies = await client.GetFromJsonAsync<DeliveryCompanyContract[]>(
            "/api/v1/delivery-companies?countryIds=2");

        Assert.Equal(
            new DeliveryCompanyContract(
                2,
                "UAE Express",
                "/static/DefaultImage.svg"),
            Assert.Single(companies!));
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

    private sealed record DeliveryCompanyContract(
        int Id,
        string Name,
        string LogoUrl);
}
