using System.Security.Cryptography;
using System.Text;
using Luxira.Api.Infrastructure.Caching;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Infrastructure.Webhooks;

public sealed class WebhookSecurity(IConfiguration configuration, LuxiraCacheService cache)
{
    public void ValidateSharedSecret(HttpRequest request, string provider)
    {
        var expected = configuration[$"Webhooks:{provider}:Secret"];
        if (string.IsNullOrWhiteSpace(expected))
            throw new InvalidOperationException($"{provider} webhook secret is not configured.");

        var supplied = request.Headers["X-Luxira-Webhook-Secret"].ToString();
        if (string.IsNullOrWhiteSpace(supplied) || !FixedTimeEquals(expected, supplied))
            throw new UnauthorizedException("Invalid webhook secret.");
    }

    public async Task<bool> ExecuteOnceAsync(
        string provider,
        string eventKey,
        Func<CancellationToken, Task> action,
        CancellationToken ct)
    {
        var keyHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(eventKey)));
        var cacheKey = $"webhooks:{provider.ToLowerInvariant()}:{keyHash}";
        return await cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken =>
            {
                await action(cancellationToken);
                return true;
            },
            TimeSpan.FromDays(7),
            tags: [$"webhooks:{provider.ToLowerInvariant()}"],
            ct: ct);
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
