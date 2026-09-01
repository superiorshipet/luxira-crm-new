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
        builder.Property(m => m.Name).HasMaxLength(100).IsRequired();
        builder.Property(m => m.DisplayName).HasMaxLength(100);
        builder.Property(m => m.Code).HasMaxLength(50);

        builder.HasMany(m => m.Products)
            .WithOne(p => p.ManufacturingCompany)
            .HasForeignKey(p => p.ManufacturingCompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MainProductDbConfig : IDbConfig<MainProduct>
{
    public void Configure(EntityTypeBuilder<MainProduct> builder)
    {
        builder.ToTable("MainProducts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.SKU).HasMaxLength(100);
        builder.Property(p => p.DefaultPrice).HasPrecision(18, 2);
        builder.Property(p => p.DefaultCost).HasPrecision(18, 2);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.MainProduct)
            .HasForeignKey(i => i.MainProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductImageDbConfig : IDbConfig<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(i => i.S3Key).HasMaxLength(450);
    }
}

public class ProductMinimumSellingPriceDbConfig : IDbConfig<ProductMinimumSellingPrice>
{
    public void Configure(EntityTypeBuilder<ProductMinimumSellingPrice> builder)
    {
        builder.ToTable("ProductMinimumSellingPrices");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.MinimumPrice).HasPrecision(18, 2);
    }
}

public class StoreCodeFolderDbConfig : IDbConfig<StoreCodeFolder>
{
    public void Configure(EntityTypeBuilder<StoreCodeFolder> builder)
    {
        builder.ToTable("StoreCodeFolders");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.FolderName).HasMaxLength(100).IsRequired();
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
