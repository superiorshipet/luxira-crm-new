using Luxira.Api.Features.Media.DTOs;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Features.Media.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.Media.Services;

public class MediaService
{
    private readonly MediaRepository _repository;
    private readonly IConfiguration _configuration;

    public MediaService(MediaRepository repository, IConfiguration configuration)
    {
        _repository = repository;
        _configuration = configuration;
    }

    public async Task<MediaObjectDto> GetMediaByKeyAsync(string s3Key, CancellationToken ct = default)
    {
        var item = await _repository.GetByKeyAsync(s3Key, ct);
        if (item == null)
        {
            throw new NotFoundException($"Media object with key {s3Key} not found.");
        }

        var bucketName = _configuration["AWS:S3:BucketName"] ?? string.Empty;
        var region = _configuration["AWS:Region"] ?? "eu-central-1";
        var publicUrl = string.IsNullOrEmpty(bucketName)
            ? null
            : $"https://{bucketName}.s3.{region}.amazonaws.com/{item.Key}";

        return new MediaObjectDto(
            item.Id,
            item.Key,
            bucketName,
            item.ContentType ?? "application/octet-stream",
            item.SizeBytes,
            item.OriginalFileName,
            publicUrl,
            item.UploadedAt
        );
    }

    public async Task<UploadMediaResponse> SaveMediaMetadataAsync(
        string s3Key,
        string bucketName,
        string contentType,
        long sizeBytes,
        string originalFileName,
        string userId,
        CancellationToken ct = default)
    {
        var baseUrl = _configuration["S3:BaseUrl"] ?? "https://media.luxiracrm.com";
        var publicUrl = $"{baseUrl.TrimEnd('/')}/{s3Key}";

        var entity = new S3StoredObject
        {
            S3Key = s3Key,
            Prefix = s3Key.Contains('/') ? s3Key[..s3Key.LastIndexOf('/')] : string.Empty,
            BucketName = bucketName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            OriginalFileName = originalFileName,
            PublicUrl = publicUrl,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(entity, ct);
        return new UploadMediaResponse(created.Id, created.S3Key, publicUrl, created.ContentType);
    }
}
