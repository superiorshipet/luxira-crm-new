using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Luxira.Api.Features.Auth.Models;
using Microsoft.IdentityModel.Tokens;

namespace Luxira.Api.Features.Auth.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(ApplicationUser user)
    {
        var key = _configuration["Jwt:Key"] ?? "super-secret-default-key-luxira-crm-jwt-secret-2026-auth";
        var issuer = _configuration["Jwt:Issuer"] ?? "Luxira.Api";
        var audience = _configuration["Jwt:Audience"] ?? "Luxira.Clients";

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
            claims.Add(new Claim(ClaimTypes.Role, user.Role));
            if (user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                user.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
                claims.Add(new Claim(ClaimTypes.Role, "ExecutiveDirector"));
            }
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return (handler.WriteToken(token), expiresAt);
    }
}
