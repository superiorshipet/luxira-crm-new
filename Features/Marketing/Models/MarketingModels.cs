namespace Luxira.Api.Features.Marketing.Models;

public class AdvertisingCampaign
{
    public int Id { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageS3Key { get; set; }
    public string? Name { get; set; }
    public int Country { get; set; }
    public int? MainWarehouseId { get; set; }
    public Luxira.Api.Features.Warehouses.Models.MainWarehouse? MainWarehouse { get; set; }
    public int? ManufacturingCompanyId { get; set; }
    public Luxira.Api.Features.ManufacturingCompanies.Models.ManufacturingCompany? ManufacturingCompany { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class AdvertisingManagerStoreFolder
{
    public int Id { get; set; }
    public int ManufacturingCompanyId { get; set; }
    public Luxira.Api.Features.ManufacturingCompanies.Models.ManufacturingCompany? ManufacturingCompany { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public ICollection<AdvertisingManagerItem> Items { get; set; } = [];
}

public class AdvertisingManagerItem
{
    public int Id { get; set; }
    public int AdvertisingManagerStoreFolderId { get; set; }
    public AdvertisingManagerStoreFolder? StoreFolder { get; set; }
    public int? StorePasswordPageId { get; set; }
    public Luxira.Api.Features.Communication.Models.StorePasswordPage? StorePasswordPage { get; set; }
    public string? FolderName { get; set; }
    public string? AccountName { get; set; }
    public string FacebookPageNameSnapshot { get; set; } = string.Empty;
    public string? EmailSnapshot { get; set; }
    public string? PasswordSnapshot { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}
public class AdvertisingManagerItemAccount { public int Id { get; set; } public int AdvertisingManagerItemId { get; set; } public string AccountName { get; set; } = string.Empty; public DateTime CreatedAt { get; set; } public string? CreatedByUserId { get; set; } public DateTime? UpdatedAt { get; set; } public string? UpdatedByUserId { get; set; } }
public class AdvertisingManagerAccountProfile { public int Id { get; set; } public int AdvertisingManagerItemId { get; set; } public string AccountKey { get; set; } = string.Empty; public string AccountStatus { get; set; } = "Active"; public DateTime CreatedAt { get; set; } public string? CreatedByUserId { get; set; } public DateTime? UpdatedAt { get; set; } public string? UpdatedByUserId { get; set; } public ICollection<AdvertisingManagerAccountLink> Links { get; set; } = []; public ICollection<AdvertisingManagerPaymentCard> PaymentCards { get; set; } = []; }
public class AdvertisingManagerAccountLink { public int Id { get; set; } public int AdvertisingManagerAccountProfileId { get; set; } public AdvertisingManagerAccountProfile? Profile { get; set; } public string LinkName { get; set; } = string.Empty; public string LinkUrl { get; set; } = string.Empty; public int SortOrder { get; set; } public DateTime CreatedAt { get; set; } }
public class AdvertisingManagerPaymentCard { public int Id { get; set; } public int AdvertisingManagerAccountProfileId { get; set; } public AdvertisingManagerAccountProfile? Profile { get; set; } public string CardholderName { get; set; } = string.Empty; public string CardLast4 { get; set; } = string.Empty; public string? CardNumberProtected { get; set; } public string? CardCvvProtected { get; set; } public string CardBrand { get; set; } = "Card"; public int ExpiryMonth { get; set; } public int ExpiryYear { get; set; } public string CardType { get; set; } = "Backup"; public int SortOrder { get; set; } public DateTime CreatedAt { get; set; } }

public class MarketingLead
{
    public int Id { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public int OrderSource { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ChatUrl { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string ApplicationUserId { get; set; } = string.Empty;
}

public class StoreScript
{
    public int Id { get; set; }
    public int StoreCodeFolderId { get; set; }
    public int ManufacturingCompanyId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string EngineVersion { get; set; } = string.Empty;
    public long RevisionStamp { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }
    public ICollection<ScriptTarget> Targets { get; set; } = [];
    public ICollection<ScriptThemeToken> ThemeTokens { get; set; } = [];
    public ICollection<ScriptSetting> Settings { get; set; } = [];
    public ICollection<ScriptCountry> Countries { get; set; } = [];
    public ICollection<ScriptCategory> Categories { get; set; } = [];
    public ICollection<ScriptTranslation> Translations { get; set; } = [];
    public ICollection<ScriptEditHistory> EditHistories { get; set; } = [];
}

public class SeedScriptSetting
{
    public int Id { get; set; }
    public string Message { get; set; } = "alert('hello world');";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ScriptGlobalSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ScriptTarget { public int Id { get; set; } public int ScriptDefinitionId { get; set; } public StoreScript? ScriptDefinition { get; set; } public string Kind { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; public int SortOrder { get; set; } public bool IsDeleted { get; set; } }
public class ScriptThemeToken { public int Id { get; set; } public int ScriptDefinitionId { get; set; } public StoreScript? ScriptDefinition { get; set; } public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; public int SortOrder { get; set; } }
public class ScriptSetting { public int Id { get; set; } public int ScriptDefinitionId { get; set; } public StoreScript? ScriptDefinition { get; set; } public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; public int SortOrder { get; set; } }
public class ScriptCountry { public int Id { get; set; } public int ScriptDefinitionId { get; set; } public StoreScript? ScriptDefinition { get; set; } public string Code { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string FlagHex { get; set; } = string.Empty; public int SortOrder { get; set; } public bool IsEnabled { get; set; } = true; public bool IsDeleted { get; set; } public ICollection<ScriptCountryValue> Values { get; set; } = []; }
public class ScriptCountryValue { public int Id { get; set; } public int ScriptCountryId { get; set; } public ScriptCountry? ScriptCountry { get; set; } public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
public class ScriptCategory
{
    public int Id { get; set; } public int ScriptDefinitionId { get; set; } public StoreScript? ScriptDefinition { get; set; }
    public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string Icon { get; set; } = string.Empty; public string IconKind { get; set; } = string.Empty;
    public int SortOrder { get; set; } public bool IsEnabled { get; set; } = true; public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } public string? CreatedByUserId { get; set; } public string? CreatedByName { get; set; } public DateTime UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; } public string? UpdatedByName { get; set; } public DateTime? DeletedAt { get; set; } public string? DeletedByUserId { get; set; } public string? DeletedByName { get; set; }
    public ICollection<ScriptSubCategory> SubCategories { get; set; } = []; public ICollection<ScriptMessage> Messages { get; set; } = [];
}
public class ScriptSubCategory
{
    public int Id { get; set; } public int ScriptCategoryId { get; set; } public ScriptCategory? ScriptCategory { get; set; } public int? ParentSubCategoryId { get; set; } public ScriptSubCategory? ParentSubCategory { get; set; }
    public string Key { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; public string Icon { get; set; } = string.Empty; public string IconKind { get; set; } = string.Empty; public string? ColorToken { get; set; }
    public int SortOrder { get; set; } public bool IsCountryScoped { get; set; } public bool IsEnabled { get; set; } = true; public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } public string? CreatedByUserId { get; set; } public string? CreatedByName { get; set; } public DateTime UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; } public string? UpdatedByName { get; set; } public DateTime? DeletedAt { get; set; } public string? DeletedByUserId { get; set; } public string? DeletedByName { get; set; }
    public ICollection<ScriptMessage> Messages { get; set; } = []; public ICollection<ScriptSubCategory> Children { get; set; } = [];
}
public class ScriptMessage { public int Id { get; set; } public int? ScriptCategoryId { get; set; } public ScriptCategory? ScriptCategory { get; set; } public int? ScriptSubCategoryId { get; set; } public ScriptSubCategory? ScriptSubCategory { get; set; } public int? ScriptCountryId { get; set; } public ScriptCountry? ScriptCountry { get; set; } public int Phase { get; set; } public int StepOrder { get; set; } public string Gender { get; set; } = string.Empty; public string Text { get; set; } = string.Empty; }
public class ScriptTranslation { public int Id { get; set; } public int ScriptDefinitionId { get; set; } public StoreScript? ScriptDefinition { get; set; } public string Lang { get; set; } = string.Empty; public string SourceText { get; set; } = string.Empty; public string TargetText { get; set; } = string.Empty; public bool IsDeleted { get; set; } }
public class ScriptEditHistory { public int Id { get; set; } public int ScriptDefinitionId { get; set; } public StoreScript? ScriptDefinition { get; set; } public string EntityType { get; set; } = string.Empty; public int EntityId { get; set; } public string Field { get; set; } = string.Empty; public string? OldValue { get; set; } public string? NewValue { get; set; } public bool IsRestoreAction { get; set; } public DateTime CreatedAt { get; set; } public string? CreatedByUserId { get; set; } public string? CreatedByName { get; set; } }

public class WebsiteDomain
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
    public Luxira.Api.Features.ManufacturingCompanies.Models.ManufacturingCompany? ManufacturingCompany { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPinned { get; set; }
}

public class WebsiteDomainEditLog
{
    public int Id { get; set; }
    public int WebsiteDomainId { get; set; }
    public string OldDomain { get; set; } = string.Empty;
    public string NewDomain { get; set; } = string.Empty;
    public int OldManufacturingCompanyId { get; set; }
    public int NewManufacturingCompanyId { get; set; }
    public bool OldIsActive { get; set; }
    public bool NewIsActive { get; set; }
    public DateTime EditedAt { get; set; }
    public string EditedByUserId { get; set; } = string.Empty;
    public bool IsRestored { get; set; }
    public DateTime? RestoredAt { get; set; }
    public string RestoredByUserId { get; set; } = string.Empty;
}

public class VideoLink
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
    public Luxira.Api.Features.ManufacturingCompanies.Models.ManufacturingCompany? ManufacturingCompany { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }
}

public class VideoLinkChangeHistory
{
    public int Id { get; set; }
    public int VideoLinkId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int? OldManufacturingCompanyId { get; set; }
    public int? NewManufacturingCompanyId { get; set; }
    public string? OldStoreName { get; set; }
    public string? NewStoreName { get; set; }
    public string? OldUrl { get; set; }
    public string? NewUrl { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
}
