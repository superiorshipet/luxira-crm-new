namespace Luxira.Api.Features.ManufacturingCompanies.Models;

public class ManufacturingCompany
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Code { get; set; }
    public int Country { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MainProduct> Products { get; set; } = new();
}

public class MainProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public decimal DefaultPrice { get; set; }
    public decimal? DefaultCost { get; set; }
    public int ManufacturingCompanyId { get; set; }
    public ManufacturingCompany? ManufacturingCompany { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ProductImage> Images { get; set; } = new();
}

public class ProductImage
{
    public int Id { get; set; }
    public int MainProductId { get; set; }
    public MainProduct? MainProduct { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? S3Key { get; set; }
    public bool IsPrimary { get; set; }
}

public class ProductMinimumSellingPrice
{
    public int Id { get; set; }
    public int MainProductId { get; set; }
    public int Country { get; set; }
    public decimal MinimumPrice { get; set; }
}

public class StoreCodeFolder
{
    public int Id { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
}

public class StoreCodeEditHistory
{
    public int Id { get; set; }
    public int StoreCodeFolderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}
