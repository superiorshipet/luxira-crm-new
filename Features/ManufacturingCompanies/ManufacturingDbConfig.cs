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
        builder.Property(s => s.PageType).HasMaxLength(100).IsRequired();
        builder.Property(s => s.CreatedByUserId).HasMaxLength(450);
        builder.Property(s => s.UpdatedByUserId).HasMaxLength(450);
        builder.Property(s => s.DeletedByUserId).HasMaxLength(450);
    }
}

public class StoreCodeEditHistoryDbConfig : IDbConfig<StoreCodeEditHistory>
{
    public void Configure(EntityTypeBuilder<StoreCodeEditHistory> builder)
    {
        builder.ToTable("StoreCodeEditHistories");
        builder.HasKey(h => h.Id);
    }
}
