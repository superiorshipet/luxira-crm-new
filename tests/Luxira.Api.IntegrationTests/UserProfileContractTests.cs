using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Luxira.Api.IntegrationTests;

public sealed class UserProfileContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task CurrentAndLegacyProfilesPreserveSelectionAndUrlRules()
    {
        using var client = CreateAuthenticatedClient();

        var current = await client.GetFromJsonAsync<ProfileContract>(
            "/api/v1/users/me/profile");
        var canonical = await client.GetFromJsonAsync<ProfileContract>(
            "/api/v1/users/integration-test-user-id/profile");
        var legacy = await client.GetFromJsonAsync<ProfileContract>(
            "/Conference/UserProfile?id=integration-test-user-id");

        var expected = new ProfileContract(
            "integration-test-user-id",
            "  Employee Display  ",
            "/Employees/avatar.png",
            "SoftwareDeveloper",
            "SoftwareDeveloper",
            "12345678");
        Assert.Equal(expected, current);
        Assert.Equal(expected, canonical);
        Assert.Equal(expected, legacy);
    }

    [Fact]
    public async Task MissingEmployeeFallsBackToIdentityRoleAndGeneratedAvatar()
    {
        using var client = CreateAuthenticatedClient();

        var result = await client.GetFromJsonAsync<ProfileContract>(
            "/api/v1/users/role-user/profile");

        Assert.Equal(
            new ProfileContract(
                "role-user",
                "role-user-name",
                "/Conference/Avatar?id=role-user",
                "CallCenter",
                "CallCenter",
                "-"),
            result);
    }

    [Fact]
    public async Task UnknownAndBlankLegacyIdsPreserveStatusAndMessages()
    {
        using var client = CreateAuthenticatedClient();

        using var unknown = await client.GetAsync(
            "/Conference/UserProfile?id=unknown");
        using var blank = await client.GetAsync(
            "/Conference/UserProfile?id=%20");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Contains("المستخدم غير موجود", await unknown.Content.ReadAsStringAsync());
        Assert.Contains("معرف المستخدم مطلوب", await blank.Content.ReadAsStringAsync());
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

    private sealed record ProfileContract(
        string Id,
        string Name,
        string Avatar,
        string Role,
        string Title,
        string Phone);
}
