using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Luxira.Api.IntegrationTests;

public sealed class RoleAliasTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly LuxiraApiFactory _factory = factory;

    [Fact]
    public async Task LegacySpacedTeamLeaderRoleGetsCanonicalAliasOnce()
    {
        using var scope = _factory.Services.CreateScope();
        var transformer = scope.ServiceProvider
            .GetRequiredService<IClaimsTransformation>();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Team Leader")],
            "Test",
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        await transformer.TransformAsync(principal);
        await transformer.TransformAsync(principal);

        Assert.True(principal.IsInRole("Team Leader"));
        Assert.True(principal.IsInRole("TeamLeader"));
        Assert.Single(
            principal.Claims,
            claim => claim.Type == ClaimTypes.Role &&
                claim.Value == "TeamLeader");
    }
}
