using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Infrastructure.Persistence;

public sealed class LuxiraReadDbContext(
    DbContextOptions<LuxiraReadDbContext> options) : DbContext(options)
{
    internal IQueryable<DeliveryCompanyReadRow> DeliveryCompanies =>
        Set<DeliveryCompanyReadRow>();

    internal IQueryable<DeliveryCompanyPriceReadRow> DeliveryCompanyPrices =>
        Set<DeliveryCompanyPriceReadRow>();

    internal IQueryable<OrderDeliveryReadRow> Orders => Set<OrderDeliveryReadRow>();

    internal IQueryable<StoreDeliveryAssignmentReadRow> StoreDeliveryAssignments =>
        Set<StoreDeliveryAssignmentReadRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new DeliveryCompanyReadRowConfiguration());
        modelBuilder.ApplyConfiguration(
            new DeliveryCompanyPriceReadRowConfiguration());
        modelBuilder.ApplyConfiguration(new OrderDeliveryReadRowConfiguration());
        modelBuilder.ApplyConfiguration(
            new StoreDeliveryAssignmentReadRowConfiguration());
    }
}

internal sealed class OrderDeliveryReadRow
{
    internal int Id { get; init; }
    internal int? ManufacturingCompanyId { get; init; }
}

internal sealed class OrderDeliveryReadRowConfiguration
    : IEntityTypeConfiguration<OrderDeliveryReadRow>
{
    public void Configure(EntityTypeBuilder<OrderDeliveryReadRow> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedOnAdd();
        builder.Property(order => order.ManufacturingCompanyId);
    }
}

internal sealed class StoreDeliveryAssignmentReadRow
{
    internal int Id { get; init; }
    internal int ManufacturingCompanyId { get; init; }
    internal int? DeliveryCompanyId { get; init; }
    internal bool IsManualTransfer { get; init; }
}

internal sealed class StoreDeliveryAssignmentReadRowConfiguration
    : IEntityTypeConfiguration<StoreDeliveryAssignmentReadRow>
{
    public void Configure(EntityTypeBuilder<StoreDeliveryAssignmentReadRow> builder)
    {
        builder.ToTable("StoreDeliveryCompanyAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedOnAdd();
        builder.Property(assignment => assignment.ManufacturingCompanyId);
        builder.Property(assignment => assignment.DeliveryCompanyId);
        builder.Property(assignment => assignment.IsManualTransfer);
    }
}

internal sealed class DeliveryCompanyPriceReadRow
{
    internal int Id { get; init; }
    internal int Country { get; init; }
    internal decimal Price { get; init; }
    internal string? City { get; init; }
    internal int DeliveryCompanyId { get; init; }
}

internal sealed class DeliveryCompanyPriceReadRowConfiguration
    : IEntityTypeConfiguration<DeliveryCompanyPriceReadRow>
{
    public void Configure(EntityTypeBuilder<DeliveryCompanyPriceReadRow> builder)
    {
        builder.ToTable("DeliveryCompanyPrices");
        builder.HasKey(price => price.Id);
        builder.Property(price => price.Id).ValueGeneratedOnAdd();
        builder.Property(price => price.Country);
        builder.Property(price => price.Price).HasColumnType("decimal(18,2)");
        builder.Property(price => price.City);
        builder.Property(price => price.DeliveryCompanyId);
    }
}

internal sealed class DeliveryCompanyReadRow
{
    internal int Id { get; init; }
    internal required string Name { get; init; }
    internal string? ImageUrl { get; init; }
    internal int Country { get; init; }
    internal string? City { get; init; }
    internal bool IsShown { get; init; }
    internal bool IsRepresentative { get; init; }
}

internal sealed class DeliveryCompanyReadRowConfiguration
    : IEntityTypeConfiguration<DeliveryCompanyReadRow>
{
    public void Configure(EntityTypeBuilder<DeliveryCompanyReadRow> builder)
    {
        builder.ToTable("DeliveryCompanies");
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Id).ValueGeneratedOnAdd();
        builder.Property(company => company.Name)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(company => company.ImageUrl);
        builder.Property(company => company.Country);
        builder.Property(company => company.City);
        builder.Property(company => company.IsShown);
        builder.Property(company => company.IsRepresentative);
    }
}
