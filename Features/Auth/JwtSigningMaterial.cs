using System.Security.Cryptography;
using System.Text;

namespace Luxira.Api.Features.Auth;

public sealed class JwtSigningMaterial
{
    private JwtSigningMaterial(
        string issuer,
        string audience,
        byte[] key,
        TimeSpan accessTokenLifetime)
    {
        Issuer = issuer;
        Audience = audience;
        Key = key;
        AccessTokenLifetime = accessTokenLifetime;
    }

    public string Issuer { get; }
    public string Audience { get; }
    public byte[] Key { get; }
    public TimeSpan AccessTokenLifetime { get; }

    public static JwtSigningMaterial Create(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var configuredKey = configuration["Jwt:Key"];

        byte[] key;
        if (!string.IsNullOrWhiteSpace(issuer) &&
            !string.IsNullOrWhiteSpace(audience) &&
            !string.IsNullOrWhiteSpace(configuredKey) &&
            Encoding.UTF8.GetByteCount(configuredKey) >= 32)
        {
            key = Encoding.UTF8.GetBytes(configuredKey);
        }
        else if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            issuer = "Luxira.Local";
            audience = "Luxira.Local.Clients";
            key = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            throw new InvalidOperationException(
                "Jwt:Issuer, Jwt:Audience, and a Jwt:Key of at least 32 UTF-8 bytes are required outside Development/Testing.");
        }

        var expiryMinutes = configuration.GetValue<int?>("Jwt:ExpiryMinutes") ??
            (10 * 24 * 60);
        if (expiryMinutes is < 5 or > 14_400)
        {
            throw new InvalidOperationException(
                "Jwt:ExpiryMinutes must be between 5 minutes and 10 days.");
        }

        return new JwtSigningMaterial(
            issuer!,
            audience!,
            key,
            TimeSpan.FromMinutes(expiryMinutes));
    }
}
