using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class SearchKeywordOptionContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private static readonly string[] LegacyFallbackCategories =
    [
        "أسعار وترتيب",
        "فترات زمنية",
        "حالات الطلبات",
        "دول ومناطق",
        "مصادر الطلبات",
        "فلاتر مخصصة",
        "عام",
    ];

    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task CanonicalAndLegacyOptionsPreserveExactCatalog()
    {
        using var client = CreateAdminClient(_factory);

        var canonical = await client.GetFromJsonAsync<OptionsContract>(
            "/api/v1/administration/search-keywords/options");
        var legacy = await client.GetFromJsonAsync<OptionsContract>(
            "/Home/GetSearchKeywordOptions");

        Assert.True(canonical!.Ok);
        Assert.Equal(canonical.Categories, legacy!.Categories);
        Assert.Equal(canonical.TargetTypes, legacy.TargetTypes);
        Assert.Equal(12, canonical.TargetTypes.Length);
        Assert.Equal(18, canonical.TypeOptions["OrderStatus"].Length);
        Assert.Equal(2, canonical.TypeOptions["OrderSource"]
            .Count(option => option.Value == "فيسبوك"));
    }

    [Fact]
    public async Task MissingReadStoreUsesExactLegacyFallbackWithoutConnecting()
    {
        await using var unavailableFactory =
            new LuxiraUnavailableInfrastructureFactory();
        using var client = CreateAdminClient(unavailableFactory);

        var result = await client.GetFromJsonAsync<OptionsContract>(
            "/api/v1/administration/search-keywords/options");

        Assert.Equal(
            LegacyFallbackCategories,
            result!.Categories);
    }

    private static HttpClient CreateAdminClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestJwtTokenFactory.Create("Admin"));
        return client;
    }

    private sealed record OptionsContract(
        bool Ok,
        string[] Categories,
        OptionContract[] TargetTypes,
        Dictionary<string, OptionContract[]> TypeOptions);

    private sealed record OptionContract(string Value, string Label);
}
