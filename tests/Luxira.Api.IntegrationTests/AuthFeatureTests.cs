using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Luxira.Api.Features.Auth.DTOs;

namespace Luxira.Api.IntegrationTests;

public sealed class AuthFeatureTests(LuxiraApiFactory factory) : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterAndLoginFlowSucceeds()
    {
        var username = $"user_{Guid.NewGuid():N}";
        var registerReq = new RegisterRequest(username, $"{username}@test.com", "Password123!", "New User", 1, 1);

        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(regResult);
        Assert.False(string.IsNullOrEmpty(regResult.Token));
        Assert.Equal(username, regResult.User.Username);

        // Login
        var loginReq = new LoginRequest(username, "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginResult);
        Assert.False(string.IsNullOrEmpty(loginResult.Token));

        // Get Profile
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);
        var profileResponse = await _client.GetAsync("/api/v1/user/me");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal(username, profile.Username);
    }
}
