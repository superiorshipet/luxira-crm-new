namespace Luxira.Api.Features.Media.Models;

public class S3StoredObject
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ETag { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedByUserId { get; set; } = string.Empty;
    public string? UploadedByUserName { get; set; }
    public int? OrderId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string S3Key { get => Key; set => Key = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string BucketName { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? PublicUrl { get; set; }
}
