using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Luxira.Api.Data;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.StaticFiles;

namespace Luxira.Api.Infrastructure.S3;

public sealed record S3ObjectInfo(string Key, long Size, DateTime LastModified);
public readonly record struct S3ObjectMetadata(long SizeBytes, string? ETag, string? ContentType);

public class S3StorageService : IDisposable
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    private readonly AmazonS3Client _s3Client;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<S3StorageService> _logger;
    private readonly string _bucket;
    private readonly string _region;
    private bool _disposed;

    public S3StorageService(
        IConfiguration configuration,
        ApplicationDbContext db,
        ILogger<S3StorageService> logger)
    {
        _db = db;
        _logger = logger;
        _bucket = configuration["AWS:S3:BucketName"] ?? "luxira-crm-media";
        _region = configuration["AWS:Region"] ?? "eu-central-1";

        var accessKey = configuration["AWS:AccessKey"];
        var secretKey = configuration["AWS:SecretKey"];

        if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
        {
            var regionEndpoint = RegionEndpoint.GetBySystemName(_region);
            _s3Client = new AmazonS3Client(accessKey, secretKey, regionEndpoint);
        }
        else
        {
            _s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(_region));
        }
    }

    public string BucketName => _bucket;
    public string Region => _region;

    public async Task<S3StoredObject> UploadAsync(
        IFormFile file,
        string prefix,
        string? userId,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Empty file.", nameof(file));

        var key = BuildKey(prefix, file.FileName);

        await using var stream = file.OpenReadStream();
        await PutAsync(stream, file.Length, key, file.ContentType, ct);

        var publicUrl = $"https://{_bucket}.s3.{_region}.amazonaws.com/{key}";

        var record = new S3StoredObject
        {
            S3Key = key,
            Prefix = prefix,
            BucketName = _bucket,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            PublicUrl = publicUrl,
            UploadedAt = IstanbulTimeHelper.Now,
            UploadedByUserId = userId ?? "system"
        };

        _db.S3StoredObjects.Add(record);
        await _db.SaveChangesAsync(ct);

        return record;
    }

    public async Task<S3StoredObject> UploadStreamAsync(
        Stream stream,
        long length,
        string prefix,
        string sourceFileName,
        string contentType,
        string? userId,
        CancellationToken ct = default)
    {
        if (stream == null || length <= 0)
            throw new ArgumentException("Empty stream.", nameof(stream));

        var key = BuildKey(prefix, sourceFileName);
        await PutAsync(stream, length, key, contentType, ct);

        var publicUrl = $"https://{_bucket}.s3.{_region}.amazonaws.com/{key}";

        var record = new S3StoredObject
        {
            S3Key = key,
            Prefix = prefix,
            BucketName = _bucket,
            OriginalFileName = sourceFileName,
            ContentType = contentType,
            SizeBytes = length,
            PublicUrl = publicUrl,
            UploadedAt = IstanbulTimeHelper.Now,
            UploadedByUserId = userId ?? "system"
        };

        _db.S3StoredObjects.Add(record);
        await _db.SaveChangesAsync(ct);

        return record;
    }

    private async Task PutAsync(Stream stream, long length, string key, string contentType, CancellationToken ct)
    {
        if (length >= 20 * 1024 * 1024) // 20 MB multipart
        {
            var transfer = new TransferUtility(_s3Client);
            await transfer.UploadAsync(new TransferUtilityUploadRequest
            {
                InputStream = stream,
                BucketName = _bucket,
                Key = key,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private
            }, ct);
        }
        else
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                CannedACL = S3CannedACL.Private
            };
            await _s3Client.PutObjectAsync(request, ct);
        }
    }

    public string GetPresignedUrl(string key, int minutes = 15)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddMinutes(minutes),
            Verb = HttpVerb.GET
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public string GetPresignedUploadUrl(string key, string contentType, int minutes = 15)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            ContentType = contentType,
            Expires = DateTime.UtcNow.AddMinutes(minutes),
            Verb = HttpVerb.PUT
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default) => DeleteAsync(key, null, ct);

    public async Task DeleteAsync(string key, string? userId, CancellationToken ct = default)
    {
        await _s3Client.DeleteObjectAsync(_bucket, key, ct);

        var existing = await _db.S3StoredObjects.FirstOrDefaultAsync(x => x.Key == key && !x.IsDeleted, ct);
        if (existing != null)
        {
            existing.IsDeleted = true;
            existing.DeletedAt = IstanbulTimeHelper.Now;
            existing.DeletedByUserId = userId;
            await _db.SaveChangesAsync(ct);
        }
    }

    public virtual Task DeleteObjectOnlyAsync(string key, CancellationToken ct = default) =>
        _s3Client.DeleteObjectAsync(_bucket, key, ct);

    public virtual async Task<S3StoredObject> UploadLocalFileAsync(
        string physicalPath,
        string prefix,
        string? originalFileName,
        string? userId,
        string? userName,
        int? orderId = null,
        bool addToIndex = true,
        string? explicitKey = null,
        CancellationToken ct = default)
    {
        var info = new FileInfo(physicalPath);
        if (!info.Exists) throw new FileNotFoundException("Local file not found.", physicalPath);
        if (info.Length == 0) throw new InvalidOperationException($"Local file is empty: {physicalPath}");

        prefix = prefix.Trim('/');
        if (explicitKey is not null &&
            (explicitKey.Contains("..", StringComparison.Ordinal) ||
             !explicitKey.StartsWith(prefix + "/", StringComparison.Ordinal) ||
             explicitKey.EndsWith('/')))
            throw new ArgumentException($"Unsafe explicit key: {explicitKey}", nameof(explicitKey));

        var key = explicitKey ?? BuildKey(prefix, originalFileName);
        var contentType = ContentTypes.TryGetContentType(info.Name, out var detected)
            ? detected
            : "application/octet-stream";
        await using var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
        await PutAsync(stream, info.Length, key, contentType, ct);

        var record = new S3StoredObject
        {
            Key = key,
            Prefix = prefix,
            OriginalFileName = originalFileName ?? info.Name,
            ContentType = contentType,
            SizeBytes = info.Length,
            UploadedAt = IstanbulTimeHelper.Now,
            UploadedByUserId = userId ?? "system",
            UploadedByUserName = userName,
            OrderId = orderId
        };

        if (addToIndex)
        {
            _db.S3StoredObjects.Add(record);
            await _db.SaveChangesAsync(ct);
        }

        return record;
    }

    public virtual async Task<S3ObjectMetadata?> TryGetObjectInfoAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3Client.GetObjectMetadataAsync(_bucket, key, ct);
            return new S3ObjectMetadata(response.ContentLength, response.ETag, response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<IReadOnlyList<S3ObjectInfo>> ListObjectsAsync(
        string? prefix = null,
        int maximum = int.MaxValue,
        CancellationToken ct = default)
    {
        maximum = Math.Max(maximum, 1);
        var objects = new List<S3ObjectInfo>(Math.Min(maximum, 1_000));
        string? continuationToken = null;

        do
        {
            var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucket,
                Prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix.TrimStart('/'),
                ContinuationToken = continuationToken,
                MaxKeys = Math.Min(1_000, maximum - objects.Count)
            }, ct);

            objects.AddRange(response.S3Objects.Select(item =>
                new S3ObjectInfo(item.Key, item.Size ?? 0, item.LastModified ?? DateTime.MinValue)));
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null && objects.Count < maximum);

        return objects;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<(byte[] Content, string ETag)> DownloadAsync(
        string key,
        CancellationToken ct = default)
    {
        using var response = await _s3Client.GetObjectAsync(_bucket, key, ct);
        await using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        return (buffer.ToArray(), response.ETag ?? string.Empty);
    }

    public async Task<(long totalBytes, int objectCount)> GetBucketMetricsAsync(CancellationToken ct = default)
    {
        var active = _db.S3StoredObjects.Where(item => !item.IsDeleted);
        var totalBytes = await active.SumAsync(s => (long?)s.SizeBytes, ct) ?? 0;
        var count = await active.CountAsync(ct);
        return (totalBytes, count);
    }

    private static string BuildKey(string prefix, string? originalFileName)
    {
        var ext = Path.GetExtension(originalFileName ?? string.Empty);
        var date = IstanbulTimeHelper.Now;
        var guid = Guid.NewGuid().ToString("N");
        return $"{prefix.Trim('/')}/{date:yyyy/MM/dd}/{guid}{ext}";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _s3Client.Dispose();
            }
            _disposed = true;
        }
    }
}
