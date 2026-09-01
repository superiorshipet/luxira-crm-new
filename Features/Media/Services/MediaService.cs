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

        return new MediaObjectDto(
            item.Id,
            item.S3Key,
            item.BucketName,
            item.ContentType,
            item.SizeBytes,
            item.OriginalFileName,
            item.PublicUrl,
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
