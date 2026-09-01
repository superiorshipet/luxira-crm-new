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
        builder.ToTable("StoreScripts");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Title).HasMaxLength(255).IsRequired();
    }
}

public class WebsiteDomainDbConfig : IDbConfig<WebsiteDomain>
{
    public void Configure(EntityTypeBuilder<WebsiteDomain> builder)
    {
        builder.ToTable("WebsiteDomains");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DomainName).HasMaxLength(255).IsRequired();
    }
}

public class VideoLinkDbConfig : IDbConfig<VideoLink>
{
    public void Configure(EntityTypeBuilder<VideoLink> builder)
    {
        builder.ToTable("VideoLinks");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Title).HasMaxLength(255).IsRequired();
        builder.Property(v => v.Url).HasMaxLength(500).IsRequired();
    }
}
