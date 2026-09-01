using Luxira.Api.Core;
using Luxira.Api.Features.Media.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Media;

public class MediaDbConfig : IDbConfig<S3StoredObject>
{
    public void Configure(EntityTypeBuilder<S3StoredObject> builder)
    {
        builder.ToTable("S3StoredObjects");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.S3Key).HasMaxLength(450).IsRequired();
        builder.Property(m => m.BucketName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(100);
        builder.Property(m => m.OriginalFileName).HasMaxLength(255);
        builder.Property(m => m.PublicUrl).HasMaxLength(1000);
    }
}
