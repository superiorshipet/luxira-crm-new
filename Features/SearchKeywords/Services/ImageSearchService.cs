using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Infrastructure.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics;

namespace Luxira.Api.Features.SearchKeywords.Services;

public sealed class ImageSearchService
{
    private const int OrderDistance = 20;
    private const int ReceiptDistance = 25;
    private const int ProductDistance = 6;
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly S3StorageService _s3;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ImageSearchService> _logger;

    public ImageSearchService(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        S3StorageService s3,
        IMemoryCache cache,
        ILogger<ImageSearchService> logger)
    {
        _db = db;
        _environment = environment;
        _s3 = s3;
        _cache = cache;
        _logger = logger;
    }

    public async Task<long?> ComputeHashAsync(IFormFile image, CancellationToken ct)
    {
        try
        {
            await using var stream = image.OpenReadStream();
            return await ComputeHashAsync(stream, ct);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Invalid image supplied to image search");
            return null;
        }
    }

    public static async Task<long?> ComputeHashAsync(Stream stream, CancellationToken ct = default)
    {
        try
        {
            using var image = await Image.LoadAsync<Rgba32>(stream, ct);
            return ComputeHash(image);
        }
        catch (Exception ex) when (ex is InvalidImageContentException or NotSupportedException)
        {
            return null;
        }
    }

    private static long ComputeHash(Image<Rgba32> source)
    {
        const int size = 32;
        using var image = source.Clone(operation => operation.Resize(size, size).Grayscale());
        var cosine = new double[size, 8];
        for (var x = 0; x < size; x++)
            for (var frequency = 0; frequency < 8; frequency++)
                cosine[x, frequency] = Math.Cos(((2 * x + 1) * frequency * Math.PI) / (2 * size));

        var rowDct = new double[8, size];
        for (var frequency = 0; frequency < 8; frequency++)
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    rowDct[frequency, y] += image[x, y].R * cosine[x, frequency];

        var dct = new double[8, 8];
        const double c0 = 0.7071067811865476;
        for (var u = 0; u < 8; u++)
            for (var v = 0; v < 8; v++)
            {
                for (var y = 0; y < size; y++)
                    dct[u, v] += rowDct[u, y] * cosine[y, v];
                dct[u, v] *= 0.25 * (u == 0 ? c0 : 1) * (v == 0 ? c0 : 1);
            }

        var average = Enumerable.Range(0, 8)
            .SelectMany(u => Enumerable.Range(0, 8).Select(v => (u, v)))
            .Where(point => point != (0, 0))
            .Average(point => dct[point.u, point.v]);
        long hash = 0;
        var bit = 0;
        for (var u = 0; u < 8; u++)
            for (var v = 0; v < 8; v++, bit++)
                if (dct[u, v] > average)
                    hash |= 1L << bit;
        return hash;
    }

    public static int HammingDistance(long left, long right) =>
        BitOperations.PopCount(unchecked((ulong)(left ^ right)));

    public async Task<int?> FindProductAsync(long hash, CancellationToken ct)
    {
        var products = await _db.MainWarehouses.AsNoTracking()
            .Where(product => product.ImageS3Key != null || product.ImageUrl != null)
            .Select(product => new { product.Id, product.ImageS3Key, product.ImageUrl })
            .ToListAsync(ct);
        return await FindClosestAsync(
            products.Select(product => new ImageCandidate(product.Id, product.ImageS3Key, product.ImageUrl, ProductDistance)),
            hash,
            ct);
    }

    public async Task<Order?> FindOrderAsync(long hash, CancellationToken ct)
    {
        var storedHashes = await _db.OrderPostImages.AsNoTracking()
            .Where(image => image.PHash.HasValue)
            .Select(image => new { image.OrderPost!.OrderId, Hash = image.PHash!.Value })
            .ToListAsync(ct);
        var storedMatch = storedHashes
            .Select(item => new { item.OrderId, Distance = HammingDistance(hash, item.Hash) })
            .Where(item => item.Distance <= OrderDistance)
            .MinBy(item => item.Distance);
        if (storedMatch is not null)
            return await _db.Orders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == storedMatch.OrderId, ct);

        var orders = await _db.Orders.AsNoTracking()
            .Where(order => order.PhotoS3Key != null || order.PhotoUrl != null ||
                            order.PaymentReceiptS3Key != null || order.PaymentReceiptUrl != null)
            .Select(order => new
            {
                order.Id,
                order.PhotoS3Key,
                order.PhotoUrl,
                order.PaymentReceiptS3Key,
                order.PaymentReceiptUrl
            })
            .ToListAsync(ct);
        var candidates = orders.SelectMany(order => new[]
        {
            new ImageCandidate(order.Id, order.PhotoS3Key, order.PhotoUrl, OrderDistance),
            new ImageCandidate(order.Id, order.PaymentReceiptS3Key, order.PaymentReceiptUrl, ReceiptDistance)
        });
        var orderId = await FindClosestAsync(candidates, hash, ct);
        if (orderId.HasValue)
            return await _db.Orders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == orderId, ct);

        var failures = await _db.OrderStatusHistories.AsNoTracking()
            .Where(history => history.OrderId.HasValue &&
                (history.FailureReasonImageS3Key != null || history.FailureReasonImageUrl != null))
            .Select(history => new
            {
                OrderId = history.OrderId!.Value,
                history.FailureReasonImageS3Key,
                history.FailureReasonImageUrl
            })
            .ToListAsync(ct);
        var failureCandidates = failures.SelectMany(history =>
            SplitReferences(history.FailureReasonImageS3Key, history.FailureReasonImageUrl)
                .Select(reference => new ImageCandidate(history.OrderId, reference.S3Key, reference.Url, OrderDistance)));
        orderId = await FindClosestAsync(failureCandidates, hash, ct);
        return orderId.HasValue
            ? await _db.Orders.AsNoTracking().FirstOrDefaultAsync(order => order.Id == orderId, ct)
            : null;
    }

    private async Task<int?> FindClosestAsync(IEnumerable<ImageCandidate> candidates, long searchHash, CancellationToken ct)
    {
        var bestDistance = int.MaxValue;
        int? bestId = null;
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var candidateHash = await GetHashAsync(candidate.S3Key, candidate.Url, ct);
            if (!candidateHash.HasValue) continue;
            var distance = HammingDistance(searchHash, candidateHash.Value);
            if (distance <= candidate.MaximumDistance && distance < bestDistance)
            {
                bestDistance = distance;
                bestId = candidate.Id;
                if (distance == 0) break;
            }
        }
        return bestId;
    }

    private async Task<long?> GetHashAsync(string? explicitS3Key, string? url, CancellationToken ct)
    {
        var s3Key = explicitS3Key ?? ResolveS3Key(url);
        if (!string.IsNullOrWhiteSpace(s3Key))
        {
            var cacheKey = $"image-search:s3:{s3Key}";
            if (_cache.TryGetValue(cacheKey, out long cached)) return cached;
            try
            {
                var download = await _s3.DownloadAsync(s3Key, ct);
                await using var stream = new MemoryStream(download.Content, writable: false);
                var hash = await ComputeHashAsync(stream, ct);
                if (hash.HasValue)
                    _cache.Set(cacheKey, hash.Value, new MemoryCacheEntryOptions
                    {
                        Size = 1,
                        SlidingExpiration = TimeSpan.FromHours(12)
                    });
                return hash;
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug(ex, "Could not read managed image {S3Key}", s3Key);
                return null;
            }
        }

        var localPath = ResolveLocalPath(url);
        if (localPath is null) return null;
        var localCacheKey = $"image-search:file:{localPath}:{File.GetLastWriteTimeUtc(localPath).Ticks}";
        if (_cache.TryGetValue(localCacheKey, out long localCached)) return localCached;
        try
        {
            await using var stream = File.OpenRead(localPath);
            var hash = await ComputeHashAsync(stream, ct);
            if (hash.HasValue)
                _cache.Set(localCacheKey, hash.Value, new MemoryCacheEntryOptions { Size = 1, SlidingExpiration = TimeSpan.FromHours(12) });
            return hash;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(ex, "Could not read local image {ImageUrl}", url);
            return null;
        }
    }

    private string? ResolveLocalPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _)) return null;
        var webRoot = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"));
        var path = Path.GetFullPath(Path.Combine(webRoot, url.Split('?', '#')[0].TrimStart('/')));
        return path.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(path)
            ? path
            : null;
    }

    private string? ResolveS3Key(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.StartsWith("/Media/File", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("/OrderPosts/Image", StringComparison.OrdinalIgnoreCase))
        {
            var marker = value.IndexOf("key=", StringComparison.OrdinalIgnoreCase);
            return marker < 0 ? null : Uri.UnescapeDataString(value[(marker + 4)..].Split('&')[0]);
        }
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.EndsWith("amazonaws.com", StringComparison.OrdinalIgnoreCase)) return null;
            var key = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            return key.StartsWith(_s3.BucketName + "/", StringComparison.OrdinalIgnoreCase)
                ? key[(_s3.BucketName.Length + 1)..]
                : key;
        }
        return value.StartsWith('/') ? null : value.Split('?', '#')[0];
    }

    private static IEnumerable<(string? S3Key, string? Url)> SplitReferences(string? s3Keys, string? urls)
    {
        var keys = (s3Keys ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var paths = (urls ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var count = Math.Max(keys.Length, paths.Length);
        for (var index = 0; index < count; index++)
            yield return (index < keys.Length ? keys[index] : null, index < paths.Length ? paths[index] : null);
    }

    private sealed record ImageCandidate(int Id, string? S3Key, string? Url, int MaximumDistance);
}
