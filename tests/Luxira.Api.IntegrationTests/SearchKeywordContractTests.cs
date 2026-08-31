using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class SearchKeywordContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task AdminCanFilterAndLegacyRouteMatchesCanonicalContract()
    {
        using var client = CreateAuthenticatedClient("Admin");
        const string query = "?search=%20بغداد%20&targetType=All&category=All";

        var canonical = await client.GetFromJsonAsync<ListContract>(
            "/api/v1/administration/search-keywords" + query);
        var legacy = await client.GetFromJsonAsync<ListContract>(
            "/Home/GetSearchKeywords" + query);

        Assert.True(canonical!.Ok);
        Assert.Equal(canonical.Ok, legacy!.Ok);
        Assert.Equal(canonical.Keywords, legacy.Keywords);
        var keyword = Assert.Single(canonical.Keywords);
        Assert.Equal(2, keyword.Id);
        Assert.Equal("City", keyword.TargetType);
        Assert.True(keyword.IsActive);
    }

    [Fact]
    public async Task NonAdminIsForbidden()
    {
        using var client = CreateAuthenticatedClient("CallCenter");

        using var response = await client.GetAsync(
            "/api/v1/administration/search-keywords");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AnonymousUserIsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/Home/GetSearchKeywords");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private sealed record ListContract(
        bool Ok,
        SearchKeywordContract[] Keywords);

    private sealed record SearchKeywordContract(
        int Id,
        string Phrase,
        string NormalizedPhrase,
        string TargetType,
        string TargetValue,
        string? DisplayLabel,
        string Category,
        bool IsActive,
        DateTime CreatedAt,
        string? CreatedBy,
        DateTime? UpdatedAt,
        string? UpdatedBy,
        bool IsSingleResult);
}
