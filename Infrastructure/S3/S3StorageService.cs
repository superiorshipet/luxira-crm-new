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

namespace Luxira.Api.Infrastructure.S3;

public class S3StorageService : IDisposable
{
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
            _s3Client = new AmazonS3Client();
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

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _s3Client.DeleteObjectAsync(_bucket, key, ct);

        var existing = await _db.S3StoredObjects.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (existing != null)
        {
            _db.S3StoredObjects.Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
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
        var totalBytes = await _db.S3StoredObjects.SumAsync(s => (long?)s.SizeBytes, ct) ?? 0;
        var count = await _db.S3StoredObjects.CountAsync(ct);
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
