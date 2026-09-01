namespace Luxira.Api.Features.Media.DTOs;

public record MediaObjectDto(
    int Id,
    string S3Key,
    string BucketName,
    string ContentType,
    long SizeBytes,
    string? OriginalFileName,
    string? PublicUrl,
    DateTime UploadedAt
);

public record UploadMediaResponse(
    int Id,
    string S3Key,
    string Url,
    string? ContentType
);
