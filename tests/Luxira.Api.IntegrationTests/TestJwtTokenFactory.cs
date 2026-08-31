using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Luxira.Api.IntegrationTests;

internal static class TestJwtTokenFactory
{
    internal static string Create(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "integration-test-user"),
            new(JwtRegisteredClaimNames.Jti, "integration-test-user-id"),
            new(ClaimTypes.NameIdentifier, "integration-test-user-id"),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: LuxiraApiFactory.JwtIssuer,
            audience: LuxiraApiFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(LuxiraApiFactory.JwtKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
