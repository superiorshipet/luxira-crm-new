using System.ComponentModel.DataAnnotations.Schema;

namespace Luxira.Api.Features.ManufacturingCompanies.Models;

/// <summary>
/// Maps to the ManufacturingCompanies table in the live DB.
/// Note: The live DB does NOT have DisplayName, Code, Notes, IsActive, or CreatedAt columns.
/// </summary>
public class ManufacturingCompany
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsShown { get; set; }
    public string? InvoiceImage { get; set; }
    public string? ImageUrl2 { get; set; }
    public string? PhoneNumber { get; set; }
    public int? MainWarehouseId { get; set; }
    public bool IsPasswordEmailStore { get; set; }
    public string? ImageS3Key { get; set; }
    public string? ImageUrl2S3Key { get; set; }
    public string? InvoiceImageS3Key { get; set; }

    [NotMapped] public string? DisplayName { get; set; }
    [NotMapped] public string? Code { get; set; }
    [NotMapped] public string? Notes { get; set; }
    [NotMapped] public bool IsActive { get; set; } = true;
    [NotMapped] public int Country { get; set; }
    [NotMapped] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [NotMapped] public List<MainProduct> Products { get; set; } = new();
}

/// <summary>
/// Maps to the MainProducts table in the live DB.
/// Note: The live DB does NOT have SKU, DefaultPrice, DefaultCost columns.
/// </summary>
public class MainProduct
{
    public int Id { get; set; }
    public int Country { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageS3Key { get; set; }
    public decimal Price { get; set; }
    public int ManufacturingCompanyId { get; set; }
    public int Quantity { get; set; }
    public string SaleType { get; set; } = string.Empty;
    public decimal MaximumSellingPrice { get; set; }
    public decimal MinimumSellingPrice { get; set; }
    public decimal DeliveryPrice { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByName { get; set; }
    public string? DeletedByUserId { get; set; }

    [NotMapped] public string? SKU { get; set; }
    [NotMapped] public decimal DefaultPrice { get => Price; set => Price = value; }
    [NotMapped] public decimal? DefaultCost { get; set; }
    [NotMapped] public ManufacturingCompany? ManufacturingCompany { get; set; }
    [NotMapped] public bool IsActive { get; set; } = true;
    [NotMapped] public List<ProductImage> Images { get; set; } = new();
}

public class ProductImage
{
    public int Id { get; set; }
    public int MainProductId { get; set; }
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

/// <summary>
/// Maps to StoreCodeFolders table — schema verified against live DB.
/// </summary>
public class StoreCodeFolder
{
    public int Id { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
    public ManufacturingCompany? ManufacturingCompany { get; set; }
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }

    public ICollection<StoreCodeEditHistory> EditHistories { get; set; } = new List<StoreCodeEditHistory>();
}

public class StoreCodeEditHistory
{
    public int Id { get; set; }
    public int StoreCodeFolderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}
