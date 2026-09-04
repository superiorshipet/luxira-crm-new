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
        builder.Property(m => m.Key).HasMaxLength(450).IsRequired();
        builder.Property(m => m.Prefix).HasMaxLength(450).IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(100);
        builder.Property(m => m.OriginalFileName).HasMaxLength(255);
    }
}

public sealed class MediaReferenceCleanupSettingDbConfig : IDbConfig<MediaReferenceCleanupSetting>
{
    public void Configure(EntityTypeBuilder<MediaReferenceCleanupSetting> builder)
    {
        builder.ToTable("MediaReferenceCleanupSettings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UpdatedBy).HasMaxLength(256);
    }
}

public sealed class MediaReferenceCleanupRunDbConfig : IDbConfig<MediaReferenceCleanupRun>
{
    public void Configure(EntityTypeBuilder<MediaReferenceCleanupRun> builder)
    {
        builder.ToTable("MediaReferenceCleanupRuns");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TriggeredBy).HasMaxLength(256).IsRequired();
        builder.Property(item => item.AbortReason).HasMaxLength(500);
        builder.Property(item => item.Error).HasMaxLength(2000);
    }
}
