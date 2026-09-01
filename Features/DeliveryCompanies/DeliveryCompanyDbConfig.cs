using Luxira.Api.Core;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.DeliveryCompanies;

public class DeliveryCompanyDbConfig : IDbConfig<DeliveryCompany>
{
    public void Configure(EntityTypeBuilder<DeliveryCompany> builder)
    {
        builder.ToTable("DeliveryCompanies");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.DisplayName).HasMaxLength(100);
        builder.Property(d => d.PhoneNumber).HasMaxLength(20);
        builder.Property(d => d.Address).HasMaxLength(250);
        builder.Property(d => d.IdNumber).HasMaxLength(50);
        
        builder.HasMany(d => d.Prices)
            .WithOne(p => p.DeliveryCompany)
            .HasForeignKey(p => p.DeliveryCompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeliveryCompanyPriceDbConfig : IDbConfig<DeliveryCompanyPrice>
{
    public void Configure(EntityTypeBuilder<DeliveryCompanyPrice> builder)
    {
        builder.ToTable("DeliveryCompanyPrices");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Price).HasPrecision(18, 2);
    }
}

public class StoreDeliveryCompanyAssignmentDbConfig : IDbConfig<StoreDeliveryCompanyAssignment>
{
    public void Configure(EntityTypeBuilder<StoreDeliveryCompanyAssignment> builder)
    {
        builder.ToTable("StoreDeliveryCompanyAssignments");
        builder.HasKey(a => a.Id);
    }
}

public class CamexCityDbConfig : IDbConfig<CamexCity>
{
    public void Configure(EntityTypeBuilder<CamexCity> builder)
    {
        builder.ToTable("CamexCities");
        builder.HasKey(c => c.Id);
    }
}

public class CamexCityMappingDbConfig : IDbConfig<CamexCityMapping>
{
    public void Configure(EntityTypeBuilder<CamexCityMapping> builder)
    {
        builder.ToTable("CamexCityMappings");
        builder.HasKey(m => m.Id);
    }
}

public class CamexStoreMappingDbConfig : IDbConfig<CamexStoreMapping>
{
    public void Configure(EntityTypeBuilder<CamexStoreMapping> builder)
    {
        builder.ToTable("CamexStoreMappings");
        builder.HasKey(m => m.Id);
    }
}
