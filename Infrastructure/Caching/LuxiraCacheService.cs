using Microsoft.Extensions.Caching.Hybrid;

namespace Luxira.Api.Infrastructure.Caching;

public sealed class LuxiraCacheService
{
    private readonly HybridCache _cache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultLocalExpiration = TimeSpan.FromMinutes(1);

    public LuxiraCacheService(HybridCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var effectiveExpiration = expiration ?? DefaultExpiration;
        return await _cache.GetOrCreateAsync(
            key,
            async cancellationToken => await factory(cancellationToken),
            new HybridCacheEntryOptions
            {
                Expiration = effectiveExpiration,
                LocalCacheExpiration = effectiveExpiration < DefaultLocalExpiration
                    ? effectiveExpiration
                    : DefaultLocalExpiration
            },
            tags,
            ct);
    }

    public ValueTask InvalidateAsync(string key, CancellationToken ct = default) =>
        _cache.RemoveAsync(key, ct);

    public ValueTask InvalidateByTagAsync(string tag, CancellationToken ct = default) =>
        _cache.RemoveByTagAsync(tag, ct);
}
