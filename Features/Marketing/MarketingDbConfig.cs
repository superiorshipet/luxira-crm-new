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

public class AdvertisingManagerStoreFolderDbConfig : IDbConfig<AdvertisingManagerStoreFolder>
{
    public void Configure(EntityTypeBuilder<AdvertisingManagerStoreFolder> builder)
    {
        builder.ToTable("AdvertisingManagerStoreFolders"); builder.HasKey(item => item.Id);
        builder.Property(item => item.CreatedByUserId).HasMaxLength(450); builder.Property(item => item.UpdatedByUserId).HasMaxLength(450);
        builder.HasOne(item => item.ManufacturingCompany).WithMany().HasForeignKey(item => item.ManufacturingCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Items).WithOne(item => item.StoreFolder).HasForeignKey(item => item.AdvertisingManagerStoreFolderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdvertisingManagerItemDbConfig : IDbConfig<AdvertisingManagerItem>
{
    public void Configure(EntityTypeBuilder<AdvertisingManagerItem> builder)
    {
        builder.ToTable("AdvertisingManagerItems"); builder.HasKey(item => item.Id);
        builder.Property(item => item.FolderName).HasMaxLength(200); builder.Property(item => item.AccountName).HasMaxLength(200); builder.Property(item => item.FacebookPageNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.EmailSnapshot).HasMaxLength(250); builder.Property(item => item.PasswordSnapshot).HasMaxLength(500); builder.Property(item => item.CreatedByUserId).HasMaxLength(450); builder.Property(item => item.UpdatedByUserId).HasMaxLength(450); builder.Property(item => item.DeletedByUserId).HasMaxLength(450);
        builder.HasOne(item => item.StorePasswordPage).WithMany().HasForeignKey(item => item.StorePasswordPageId).OnDelete(DeleteBehavior.SetNull);
    }
}
public class AdvertisingManagerItemAccountDbConfig : IDbConfig<AdvertisingManagerItemAccount> { public void Configure(EntityTypeBuilder<AdvertisingManagerItemAccount> builder) { builder.ToTable("AdvertisingManagerItemAccounts"); builder.HasKey(x => x.Id); builder.Property(x => x.AccountName).HasMaxLength(200).IsRequired(); builder.Property(x => x.CreatedByUserId).HasMaxLength(450); builder.Property(x => x.UpdatedByUserId).HasMaxLength(450); } }
public class AdvertisingManagerAccountProfileDbConfig : IDbConfig<AdvertisingManagerAccountProfile> { public void Configure(EntityTypeBuilder<AdvertisingManagerAccountProfile> builder) { builder.ToTable("AdvertisingManagerAccountProfiles"); builder.HasKey(x => x.Id); builder.Property(x => x.AccountKey).HasMaxLength(80).IsRequired(); builder.Property(x => x.AccountStatus).HasMaxLength(20).IsRequired(); builder.Property(x => x.CreatedByUserId).HasMaxLength(450); builder.Property(x => x.UpdatedByUserId).HasMaxLength(450); builder.HasMany(x => x.Links).WithOne(x => x.Profile).HasForeignKey(x => x.AdvertisingManagerAccountProfileId).OnDelete(DeleteBehavior.Cascade); builder.HasMany(x => x.PaymentCards).WithOne(x => x.Profile).HasForeignKey(x => x.AdvertisingManagerAccountProfileId).OnDelete(DeleteBehavior.Cascade); } }
public class AdvertisingManagerAccountLinkDbConfig : IDbConfig<AdvertisingManagerAccountLink> { public void Configure(EntityTypeBuilder<AdvertisingManagerAccountLink> builder) { builder.ToTable("AdvertisingManagerAccountLinks"); builder.HasKey(x => x.Id); builder.Property(x => x.LinkName).HasMaxLength(200).IsRequired(); builder.Property(x => x.LinkUrl).HasMaxLength(2000).IsRequired(); } }
public class AdvertisingManagerPaymentCardDbConfig : IDbConfig<AdvertisingManagerPaymentCard> { public void Configure(EntityTypeBuilder<AdvertisingManagerPaymentCard> builder) { builder.ToTable("AdvertisingManagerPaymentCards"); builder.HasKey(x => x.Id); builder.Property(x => x.CardholderName).HasMaxLength(200).IsRequired(); builder.Property(x => x.CardLast4).HasMaxLength(4).IsRequired(); builder.Property(x => x.CardNumberProtected).HasColumnType("nvarchar(max)"); builder.Property(x => x.CardCvvProtected).HasColumnType("nvarchar(max)"); builder.Property(x => x.CardBrand).HasMaxLength(80).IsRequired(); builder.Property(x => x.CardType).HasMaxLength(20).IsRequired(); } }

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
        builder.Property(s => s.Platform).HasMaxLength(20).IsRequired();
        builder.Property(s => s.EngineVersion).HasMaxLength(40).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(500);
        builder.HasIndex(s => s.StoreCodeFolderId).IsUnique();
        builder.HasMany(s => s.Targets).WithOne(s => s.ScriptDefinition).HasForeignKey(s => s.ScriptDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.ThemeTokens).WithOne(s => s.ScriptDefinition).HasForeignKey(s => s.ScriptDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Settings).WithOne(s => s.ScriptDefinition).HasForeignKey(s => s.ScriptDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Countries).WithOne(s => s.ScriptDefinition).HasForeignKey(s => s.ScriptDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Categories).WithOne(s => s.ScriptDefinition).HasForeignKey(s => s.ScriptDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Translations).WithOne(s => s.ScriptDefinition).HasForeignKey(s => s.ScriptDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.EditHistories).WithOne(s => s.ScriptDefinition).HasForeignKey(s => s.ScriptDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ScriptTargetDbConfig : IDbConfig<ScriptTarget> { public void Configure(EntityTypeBuilder<ScriptTarget> builder) { builder.ToTable("ScriptTargets"); builder.HasKey(x => x.Id); builder.Property(x => x.Kind).HasMaxLength(20).IsRequired(); builder.Property(x => x.Value).HasMaxLength(64).IsRequired(); } }
public class ScriptThemeTokenDbConfig : IDbConfig<ScriptThemeToken> { public void Configure(EntityTypeBuilder<ScriptThemeToken> builder) { builder.ToTable("ScriptThemeTokens"); builder.HasKey(x => x.Id); builder.Property(x => x.Key).HasMaxLength(64).IsRequired(); builder.Property(x => x.Value).HasMaxLength(64).IsRequired(); } }
public class ScriptSettingDbConfig : IDbConfig<ScriptSetting> { public void Configure(EntityTypeBuilder<ScriptSetting> builder) { builder.ToTable("ScriptSettings"); builder.HasKey(x => x.Id); builder.Property(x => x.Key).HasMaxLength(64).IsRequired(); builder.Property(x => x.Value).HasMaxLength(256).IsRequired(); } }
public class ScriptCountryDbConfig : IDbConfig<ScriptCountry> { public void Configure(EntityTypeBuilder<ScriptCountry> builder) { builder.ToTable("ScriptCountries"); builder.HasKey(x => x.Id); builder.Property(x => x.Code).HasMaxLength(8).IsRequired(); builder.Property(x => x.Label).HasMaxLength(120).IsRequired(); builder.Property(x => x.FlagHex).HasMaxLength(32).IsRequired(); builder.HasMany(x => x.Values).WithOne(x => x.ScriptCountry).HasForeignKey(x => x.ScriptCountryId).OnDelete(DeleteBehavior.Cascade); } }
public class ScriptCountryValueDbConfig : IDbConfig<ScriptCountryValue> { public void Configure(EntityTypeBuilder<ScriptCountryValue> builder) { builder.ToTable("ScriptCountryValues"); builder.HasKey(x => x.Id); builder.Property(x => x.Key).HasMaxLength(64).IsRequired(); builder.Property(x => x.Value).IsRequired(); } }
public class ScriptCategoryDbConfig : IDbConfig<ScriptCategory>
{
    public void Configure(EntityTypeBuilder<ScriptCategory> builder) { builder.ToTable("ScriptCategories"); builder.HasKey(x => x.Id); builder.Property(x => x.Key).HasMaxLength(64).IsRequired(); builder.Property(x => x.Label).HasMaxLength(200).IsRequired(); builder.Property(x => x.Icon).HasMaxLength(64).IsRequired(); builder.Property(x => x.IconKind).HasMaxLength(16).IsRequired(); builder.HasMany(x => x.SubCategories).WithOne(x => x.ScriptCategory).HasForeignKey(x => x.ScriptCategoryId).OnDelete(DeleteBehavior.Cascade); builder.HasMany(x => x.Messages).WithOne(x => x.ScriptCategory).HasForeignKey(x => x.ScriptCategoryId).OnDelete(DeleteBehavior.Restrict); }
}
public class ScriptSubCategoryDbConfig : IDbConfig<ScriptSubCategory>
{
    public void Configure(EntityTypeBuilder<ScriptSubCategory> builder) { builder.ToTable("ScriptSubCategories"); builder.HasKey(x => x.Id); builder.Property(x => x.Key).HasMaxLength(64).IsRequired(); builder.Property(x => x.Label).HasMaxLength(300).IsRequired(); builder.Property(x => x.Icon).HasMaxLength(64).IsRequired(); builder.Property(x => x.IconKind).HasMaxLength(16).IsRequired(); builder.Property(x => x.ColorToken).HasMaxLength(64); builder.HasOne(x => x.ParentSubCategory).WithMany(x => x.Children).HasForeignKey(x => x.ParentSubCategoryId).OnDelete(DeleteBehavior.Restrict); builder.HasMany(x => x.Messages).WithOne(x => x.ScriptSubCategory).HasForeignKey(x => x.ScriptSubCategoryId).OnDelete(DeleteBehavior.Cascade); }
}
public class ScriptMessageDbConfig : IDbConfig<ScriptMessage> { public void Configure(EntityTypeBuilder<ScriptMessage> builder) { builder.ToTable("ScriptMessages"); builder.HasKey(x => x.Id); builder.Property(x => x.Gender).HasMaxLength(1).IsRequired(); builder.Property(x => x.Text).IsRequired(); builder.HasOne(x => x.ScriptCountry).WithMany().HasForeignKey(x => x.ScriptCountryId).OnDelete(DeleteBehavior.Restrict); } }
public class ScriptTranslationDbConfig : IDbConfig<ScriptTranslation> { public void Configure(EntityTypeBuilder<ScriptTranslation> builder) { builder.ToTable("ScriptTranslations"); builder.HasKey(x => x.Id); builder.Property(x => x.Lang).HasMaxLength(8).IsRequired(); builder.Property(x => x.SourceText).IsRequired(); builder.Property(x => x.TargetText).IsRequired(); } }
public class ScriptEditHistoryDbConfig : IDbConfig<ScriptEditHistory> { public void Configure(EntityTypeBuilder<ScriptEditHistory> builder) { builder.ToTable("ScriptEditHistories"); builder.HasKey(x => x.Id); builder.Property(x => x.EntityType).HasMaxLength(64).IsRequired(); builder.Property(x => x.Field).HasMaxLength(64).IsRequired(); } }

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
