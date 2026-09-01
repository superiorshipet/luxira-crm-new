namespace Luxira.Api.Features.Marketing.Models;

public class AdvertisingCampaign
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = "Facebook"; // Facebook, TikTok, Snapchat, Google
    public decimal Budget { get; set; }
    public decimal Spent { get; set; }
    public int TargetCountry { get; set; }
    public int? ManufacturingCompanyId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
}

public class MarketingLead
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Source { get; set; }
    public int Country { get; set; }
    public string Status { get; set; } = "New"; // New, Contacted, Qualified, Converted, Lost
    public string? AssignedUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
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
