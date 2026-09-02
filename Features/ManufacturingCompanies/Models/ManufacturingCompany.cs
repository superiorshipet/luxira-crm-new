namespace Luxira.Api.Features.ManufacturingCompanies.Models;

/// <summary>
/// Maps to the ManufacturingCompanies table in the live DB.
/// Uses only columns and relationships that exist in the legacy schema.
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

    public ICollection<MainProduct> Products { get; set; } = new List<MainProduct>();
}

/// <summary>
/// Maps to the MainProducts table in the live DB.
/// Uses the legacy product pricing and soft-delete columns.
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

    public ManufacturingCompany? ManufacturingCompany { get; set; }
}

/// <summary>
/// Legacy product-media catalogue. It is related to a store and product name,
/// not to a MainProduct row.
/// </summary>
public class ProductImage
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
    public ManufacturingCompany? ManufacturingCompany { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedByUserId { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedAt { get; set; }
    public string? PinnedByUserId { get; set; }
    public string? PinnedByName { get; set; }
    public int CopyCount { get; set; }
    public DateTime? LastCopiedAt { get; set; }
    public string? CreatedByName { get; set; }
}

public class ProductMinimumSellingPrice
{
    public int Id { get; set; }
    public int Country { get; set; }
    public int ManufacturingCompanyId { get; set; }
    public int MainWarehouseId { get; set; }
    public decimal MinimumSellingPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int MainProductId { get => MainWarehouseId; set => MainWarehouseId = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MinimumPrice { get => MinimumSellingPrice; set => MinimumSellingPrice = value; }
}

public class CountryMinimumPrice
{
    public int Id { get; set; }
    public int Country { get; set; }
    public int? ManufacturingCompanyId { get; set; }
    public ManufacturingCompany? ManufacturingCompany { get; set; }
    public decimal MinimumPriceForOffers { get; set; }
    public decimal? MaximumPriceForOffers { get; set; }
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
    public int ManufacturingCompanyId { get; set; }
    public string? FileName { get; set; }
    public int LineNumber { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public bool IsRestoreAction { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}
