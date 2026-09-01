using Luxira.Api.Core;
using Luxira.Api.Features.Warehouses.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Warehouses;

public class WarehouseDbConfig : IDbConfig<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).HasMaxLength(255);
        builder.Property(w => w.Price).HasPrecision(18, 2);
        builder.Property(w => w.City).HasMaxLength(100);

        builder.HasOne(w => w.MainWarehouse)
            .WithMany(m => m.SubWarehouses)
            .HasForeignKey(w => w.MainWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MainWarehouseDbConfig : IDbConfig<MainWarehouse>
{
    public void Configure(EntityTypeBuilder<MainWarehouse> builder)
    {
        builder.ToTable("MainWarehouses");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).HasMaxLength(100).IsRequired();
        builder.Property(m => m.ImageS3Key).HasMaxLength(450);
    }
}

public class SubWarehouseDbConfig : IDbConfig<SubWarehouse>
{
    public void Configure(EntityTypeBuilder<SubWarehouse> builder)
    {
        builder.ToTable("SubWarehouses");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
    }
}

public class ManufacturingCompanyMainWarehouseDbConfig : IDbConfig<ManufacturingCompanyMainWarehouse>
{
    public void Configure(EntityTypeBuilder<ManufacturingCompanyMainWarehouse> builder)
    {
        builder.ToTable("ManufacturingCompanyMainWarehouses");
        builder.HasKey(m => m.Id);
    }
}

public class WarehouseEditHistoryDbConfig : IDbConfig<WarehouseEditHistory>
{
    public void Configure(EntityTypeBuilder<WarehouseEditHistory> builder)
    {
        builder.ToTable("WarehouseEditHistories");
        builder.HasKey(h => h.Id);
    }
}
