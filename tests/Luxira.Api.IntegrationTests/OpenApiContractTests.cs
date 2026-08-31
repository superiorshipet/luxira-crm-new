using System.Text.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class OpenApiContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EveryPublishedOperationHasAUniqueOperationId()
    {
        await using var stream = await _client.GetStreamAsync(
            "/swagger/v1/swagger.json");
        using var document = await JsonDocument.ParseAsync(stream);

        var operationIds = document.RootElement
            .GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(operation => IsHttpMethod(operation.Name))
            .Select(operation => operation.Value.GetProperty("operationId").GetString())
            .ToArray();

        Assert.DoesNotContain(operationIds, string.IsNullOrWhiteSpace);
        Assert.Equal(operationIds.Length, operationIds.Distinct().Count());
        Assert.Equal(17, operationIds.Length);
    }

    [Fact]
    public async Task OpenApiMarksOnlyProtectedOperationsWithBearerSecurity()
    {
        await using var stream = await _client.GetStreamAsync(
            "/swagger/v1/swagger.json");
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        var bearer = root
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        var publicCountries = root
            .GetProperty("paths")
            .GetProperty("/api/v1/reference-data/countries")
            .GetProperty("get");
        var protectedSources = root
            .GetProperty("paths")
            .GetProperty("/api/v1/reference-data/order-sources")
            .GetProperty("get");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.False(publicCountries.TryGetProperty("security", out _));
        Assert.Equal(
            "Bearer",
            protectedSources
                .GetProperty("security")[0]
                .EnumerateObject()
                .Single()
                .Name);
    }

    private static bool IsHttpMethod(string value) =>
        value is "get" or "post" or "put" or "patch" or "delete" or
            "head" or "options" or "trace";
}
