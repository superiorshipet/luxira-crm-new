using Luxira.Api.Core;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.ManufacturingCompanies;

public class ManufacturingCompanyDbConfig : IDbConfig<ManufacturingCompany>
{
    public void Configure(EntityTypeBuilder<ManufacturingCompany> builder)
    {
        builder.ToTable("ManufacturingCompanies");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired();
        builder.Property(m => m.ImageUrl).HasMaxLength(200);
        builder.Property(m => m.ImageS3Key).HasMaxLength(450);
        builder.Property(m => m.ImageUrl2S3Key).HasMaxLength(450);
        builder.Property(m => m.InvoiceImageS3Key).HasMaxLength(450);
        builder.HasMany(m => m.Products)
            .WithOne(p => p.ManufacturingCompany)
            .HasForeignKey(p => p.ManufacturingCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MainProductDbConfig : IDbConfig<MainProduct>
{
    public void Configure(EntityTypeBuilder<MainProduct> builder)
    {
        builder.ToTable("MainProducts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.MaximumSellingPrice).HasPrecision(18, 2);
        builder.Property(p => p.MinimumSellingPrice).HasPrecision(18, 2);
        builder.Property(p => p.DeliveryPrice).HasPrecision(18, 2);
        builder.Property(p => p.ImageS3Key).HasMaxLength(450);
    }
}

public class ProductPriceEditHistoryDbConfig : IDbConfig<ProductPriceEditHistory>
{
    public void Configure(EntityTypeBuilder<ProductPriceEditHistory> builder)
    {
        builder.ToTable("ProductPriceEditHistories");
        builder.HasKey(history => history.Id);
        builder.HasIndex(history => history.MainProductId);
        builder.HasOne(history => history.MainProduct).WithMany().HasForeignKey(history => history.MainProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductImageDbConfig : IDbConfig<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(i => i.ProductName).HasMaxLength(255).IsRequired();
        builder.HasOne(i => i.ManufacturingCompany)
            .WithMany()
            .HasForeignKey(i => i.ManufacturingCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductImageDraftDbConfig : IDbConfig<ProductImageDraft>
{
    public void Configure(EntityTypeBuilder<ProductImageDraft> builder)
    {
        builder.ToTable("ProductImageDrafts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ImageUrl).IsRequired();
        builder.Property(item => item.ProductName).HasMaxLength(255).IsRequired();
        builder.HasOne(item => item.ManufacturingCompany).WithMany()
            .HasForeignKey(item => item.ManufacturingCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductImageUserPinDbConfig : IDbConfig<ProductImageUserPin>
{
    public void Configure(EntityTypeBuilder<ProductImageUserPin> builder)
    {
        builder.ToTable("ProductImageUserPins");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ApplicationUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(item => new { item.ProductImageId, item.ApplicationUserId }).IsUnique();
        builder.HasOne(item => item.ProductImage).WithMany()
            .HasForeignKey(item => item.ProductImageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmployeeManufacturingCompanyDbConfig : IDbConfig<EmployeeManufacturingCompany>
{
    public void Configure(EntityTypeBuilder<EmployeeManufacturingCompany> builder)
    {
        builder.ToTable("EmployeeManufacturingCompany");
        builder.HasKey(item => new { item.ApplicationUserId, item.ManufacturingCompanyId });
        builder.Property(item => item.ApplicationUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(item => item.EmployeeId);
        builder.HasIndex(item => item.ManufacturingCompanyId);
    }
}

public class ProductMinimumSellingPriceDbConfig : IDbConfig<ProductMinimumSellingPrice>
{
    public void Configure(EntityTypeBuilder<ProductMinimumSellingPrice> builder)
    {
        builder.ToTable("ProductMinimumSellingPrices");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.MinimumSellingPrice).HasPrecision(18, 2);
    }
}

public class CountryMinimumPriceDbConfig : IDbConfig<CountryMinimumPrice>
{
    public void Configure(EntityTypeBuilder<CountryMinimumPrice> builder)
    {
        builder.ToTable("CountryMinimumPrices");
        builder.HasKey(price => price.Id);
        builder.Property(price => price.MinimumPriceForOffers).HasPrecision(18, 2);
        builder.Property(price => price.MaximumPriceForOffers).HasPrecision(18, 2);
        builder.HasOne(price => price.ManufacturingCompany)
            .WithMany()
            .HasForeignKey(price => price.ManufacturingCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StoreCodeFolderDbConfig : IDbConfig<StoreCodeFolder>
{
    public void Configure(EntityTypeBuilder<StoreCodeFolder> builder)
    {
        builder.ToTable("StoreCodeFolders");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.FolderName).HasMaxLength(150).IsRequired();
        builder.Property(s => s.PageType).HasMaxLength(100).HasDefaultValue(string.Empty).IsRequired();
        builder.Property(s => s.CreatedByUserId).HasMaxLength(450);
        builder.Property(s => s.UpdatedByUserId).HasMaxLength(450);
        builder.Property(s => s.DeletedByUserId).HasMaxLength(450);
        builder.HasIndex(s => s.IsDeleted).HasDatabaseName("IX_StoreCodeFolders_IsDeleted");
        builder.HasIndex(s => s.ManufacturingCompanyId).HasDatabaseName("IX_StoreCodeFolders_ManufacturingCompanyId");
        builder.HasOne(s => s.ManufacturingCompany).WithMany().HasForeignKey(s => s.ManufacturingCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StoreCodeEditHistoryDbConfig : IDbConfig<StoreCodeEditHistory>
{
    public void Configure(EntityTypeBuilder<StoreCodeEditHistory> builder)
    {
        builder.ToTable("StoreCodeEditHistories");
        builder.HasKey(h => h.Id);
        builder.HasIndex(h => new { h.ManufacturingCompanyId, h.CreatedAt }).HasDatabaseName("IX_StoreCodeEditHistories_Company_CreatedAt");
        builder.HasIndex(h => new { h.StoreCodeFolderId, h.CreatedAt }).HasDatabaseName("IX_StoreCodeEditHistories_Folder_CreatedAt");
        builder.HasOne(h => h.StoreCodeFolder).WithMany(folder => folder.EditHistories).HasForeignKey(h => h.StoreCodeFolderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StoreCodeStoreGroupDbConfig : IDbConfig<StoreCodeStoreGroup>
{
    public void Configure(EntityTypeBuilder<StoreCodeStoreGroup> builder)
    {
        builder.ToTable("StoreCodeStoreGroups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.CreatedByUserId).HasMaxLength(450);
        builder.Property(group => group.CreatedByName).HasMaxLength(250);
        builder.HasIndex(group => group.ManufacturingCompanyId).IsUnique().HasDatabaseName("IX_StoreCodeStoreGroups_ManufacturingCompanyId");
        builder.HasOne(group => group.ManufacturingCompany).WithMany().HasForeignKey(group => group.ManufacturingCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
