using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class AuthenticationContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task ProtectedEndpointReturnsProblemDetailsWithoutToken()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/v1/reference-data/order-sources");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LegacyCompatibleJwtCanAccessProtectedReferenceData()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create("CallCenter"));

        using var canonicalResponse = await client.GetAsync(
            "/api/v1/reference-data/order-sources");
        using var legacyResponse = await client.GetAsync(
            "/DataList/GetAllOrderSources");
        var authenticationFailure = string.Join(
            " ",
            canonicalResponse.Headers.WwwAuthenticate.Select(value => value.ToString()));
        var failureBody = await canonicalResponse.Content.ReadAsStringAsync();

        Assert.True(
            canonicalResponse.IsSuccessStatusCode,
            $"JWT authentication failed: {authenticationFailure} {failureBody}");
        Assert.True(
            legacyResponse.IsSuccessStatusCode,
            "The legacy route rejected a JWT accepted by the canonical route.");

        var canonical = await canonicalResponse.Content
            .ReadFromJsonAsync<OrderSourceContract[]>();
        var legacy = await legacyResponse.Content
            .ReadFromJsonAsync<OrderSourceContract[]>();

        Assert.NotNull(canonical);
        Assert.Equal(10, canonical.Length);
        Assert.Equal(
            new OrderSourceContract(
                1,
                "فيسبوك",
                "/socialmediaicons/facebook.svg"),
            canonical[0]);
        Assert.Equal(canonical, legacy);
    }

    private sealed record OrderSourceContract(
        int Id,
        string Name,
        string LogoUrl);
}
