namespace Luxira.Api.Features.Marketing.Models;

public class AdvertisingCampaign
{
    public int Id { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageS3Key { get; set; }
    public string? Name { get; set; }
    public int Country { get; set; }
    public int? MainWarehouseId { get; set; }
    public int? ManufacturingCompanyId { get; set; }
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
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Greeting, Objections, Closing, Upsell
    public int? ProductId { get; set; }
    public int? Country { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class WebsiteDomain
{
    public int Id { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public string? TargetStore { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class VideoLink
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int? ProductId { get; set; }
    public string Platform { get; set; } = "YouTube";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
