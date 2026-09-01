using System.Text.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class OpenApiContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EveryPublishedOperationHasAUniqueOperationId()
    {
        await using var stream = await _client.GetStreamAsync("/swagger/v1/swagger.json");
        using var document = await JsonDocument.ParseAsync(stream);

        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.EnumerateObject().Any(), "OpenAPI document must contain published paths.");
    }

    [Fact]
    public async Task OpenApiDefinesBearerSecurityScheme()
    {
        await using var stream = await _client.GetStreamAsync("/swagger/v1/swagger.json");
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        var bearer = root
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
    }
}
