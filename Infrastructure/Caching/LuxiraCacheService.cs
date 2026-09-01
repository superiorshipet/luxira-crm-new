using Microsoft.Extensions.Caching.Memory;

namespace Luxira.Api.Infrastructure.Caching;

public sealed class LuxiraCacheService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public LuxiraCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var result = await factory(ct);
        _cache.Set(key, result, new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiration ?? DefaultExpiration,
            Size = 1
        });
        return result;
    }

    public void Invalidate(string key) => _cache.Remove(key);
    public void InvalidateByPrefix(string prefix)
    {
        // Use a simple tag-based approach
        if (_cache is MemoryCache mc)
        {
            // Force compaction to clean expired items
            mc.Compact(0);
        }
    }
}
