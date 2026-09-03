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
}

public class WebsiteDomain
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsPinned { get; set; }
}

public class VideoLink
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
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
