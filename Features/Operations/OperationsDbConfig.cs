using Luxira.Api.Core;
using Luxira.Api.Features.Operations.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Operations;

public sealed class AppLogDbConfig : IDbConfig<AppLog>
{
    public void Configure(EntityTypeBuilder<AppLog> builder)
    {
        builder.ToTable("AppLogs");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Level).HasMaxLength(32).IsRequired();
        builder.Property(item => item.Category).HasMaxLength(256).IsRequired();
        builder.Property(item => item.Message).IsRequired();
        builder.Property(item => item.Type).HasMaxLength(32);
        builder.Property(item => item.Kind).HasMaxLength(64);
        builder.HasIndex(item => item.CreatedAtUtc);
        builder.HasIndex(item => new { item.Type, item.Kind, item.CreatedAtUtc });
    }
}

public sealed class AppMetricDbConfig : IDbConfig<AppMetric>
{
    public void Configure(EntityTypeBuilder<AppMetric> builder)
    {
        builder.ToTable("AppMetrics");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Path).HasMaxLength(300);
        builder.Property(item => item.UserName).HasMaxLength(128);
        builder.Property(item => item.Label).HasMaxLength(400);
        builder.HasIndex(item => item.CreatedAtUtc);
        builder.HasIndex(item => new { item.Kind, item.CreatedAtUtc });
    }
}
