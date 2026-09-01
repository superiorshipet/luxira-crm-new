using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Luxira.Api.Features.Auth.Models;
using Microsoft.IdentityModel.Tokens;

namespace Luxira.Api.Features.Auth.Services;

public class JwtService
{
    private readonly JwtSigningMaterial _signingMaterial;

    public JwtService(JwtSigningMaterial signingMaterial)
    {
        _signingMaterial = signingMaterial;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("AccessId", user.AcessId.ToString(CultureInfo.InvariantCulture)),
            new("CountryId", (user.Country ?? 0).ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrEmpty(user.Role))
        {
            claims.Add(new Claim("role", user.Role));
            claims.Add(new Claim(ClaimTypes.Role, user.Role));

            if (user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                user.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim("role", "Admin"));
                claims.Add(new Claim("role", "Administrator"));
                claims.Add(new Claim("role", "ExecutiveDirector"));
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
                claims.Add(new Claim(ClaimTypes.Role, "ExecutiveDirector"));
            }
        }

        var securityKey = new SymmetricSecurityKey(_signingMaterial.Key);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.Add(_signingMaterial.AccessTokenLifetime);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _signingMaterial.Issuer,
            Audience = _signingMaterial.Audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return (handler.WriteToken(token), expiresAt);
    }
}
