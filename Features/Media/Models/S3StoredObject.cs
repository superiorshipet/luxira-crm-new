namespace Luxira.Api.Features.Media.Models;

public class S3StoredObject
{
    public int Id { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? OriginalFileName { get; set; }
    public string? PublicUrl { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedByUserId { get; set; } = string.Empty;
}
