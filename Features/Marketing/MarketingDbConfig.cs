using Luxira.Api.Core;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Marketing;

public class AdvertisingCampaignDbConfig : IDbConfig<AdvertisingCampaign>
{
    public void Configure(EntityTypeBuilder<AdvertisingCampaign> builder)
    {
        builder.ToTable("AdvertisingCampaigns");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Budget).HasPrecision(18, 2);
        builder.Property(c => c.Spent).HasPrecision(18, 2);
    }
}

public class MarketingLeadDbConfig : IDbConfig<MarketingLead>
{
    public void Configure(EntityTypeBuilder<MarketingLead> builder)
    {
        builder.ToTable("MarketingLeads");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(255).IsRequired();
        builder.Property(l => l.Phone).HasMaxLength(50).IsRequired();
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
