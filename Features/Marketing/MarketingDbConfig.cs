using Luxira.Api.Core;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Marketing;

public class AdvertisingCampaignDbConfig : IDbConfig<AdvertisingCampaign>
{
    public void Configure(EntityTypeBuilder<AdvertisingCampaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ImageUrl).HasMaxLength(500);
        builder.Property(c => c.ImageS3Key).HasMaxLength(450);
        builder.HasOne(c => c.MainWarehouse).WithMany().HasForeignKey(c => c.MainWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ManufacturingCompany).WithMany().HasForeignKey(c => c.ManufacturingCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MarketingLeadDbConfig : IDbConfig<MarketingLead>
{
    public void Configure(EntityTypeBuilder<MarketingLead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.SourceName).HasMaxLength(255).IsRequired();
        builder.Property(l => l.PhoneNumber).HasMaxLength(50);
    }
}

public class StoreScriptDbConfig : IDbConfig<StoreScript>
{
    public void Configure(EntityTypeBuilder<StoreScript> builder)
    {
        builder.ToTable("ScriptDefinitions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Platform).HasMaxLength(40).IsRequired();
        builder.Property(s => s.EngineVersion).HasMaxLength(40).IsRequired();
    }
}

public class WebsiteDomainDbConfig : IDbConfig<WebsiteDomain>
{
    public void Configure(EntityTypeBuilder<WebsiteDomain> builder)
    {
        builder.ToTable("WebsiteDomains");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Domain).HasMaxLength(255).IsRequired();
        builder.HasOne(d => d.ManufacturingCompany).WithMany().HasForeignKey(d => d.ManufacturingCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WebsiteDomainEditLogDbConfig : IDbConfig<WebsiteDomainEditLog>
{
    public void Configure(EntityTypeBuilder<WebsiteDomainEditLog> builder)
    {
        builder.ToTable("WebsiteDomainEditLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.OldDomain).HasMaxLength(300).IsRequired();
        builder.Property(log => log.NewDomain).HasMaxLength(300).IsRequired();
        builder.Property(log => log.EditedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(log => log.RestoredByUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(log => new { log.WebsiteDomainId, log.EditedAt });
    }
}

public class VideoLinkDbConfig : IDbConfig<VideoLink>
{
    public void Configure(EntityTypeBuilder<VideoLink> builder)
    {
        builder.ToTable("VideoLinks");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Url).HasMaxLength(500).IsRequired();
        builder.HasOne(v => v.ManufacturingCompany).WithMany().HasForeignKey(v => v.ManufacturingCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VideoLinkChangeHistoryDbConfig : IDbConfig<VideoLinkChangeHistory>
{
    public void Configure(EntityTypeBuilder<VideoLinkChangeHistory> builder)
    {
        builder.ToTable("VideoLinkChangeHistories");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Action).HasMaxLength(30).IsRequired();
        builder.HasIndex(history => history.ChangedAt);
        builder.HasIndex(history => history.VideoLinkId);
    }
}
