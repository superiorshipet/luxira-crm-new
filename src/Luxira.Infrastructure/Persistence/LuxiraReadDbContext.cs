using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Infrastructure.Persistence;

public sealed class LuxiraReadDbContext(
    DbContextOptions<LuxiraReadDbContext> options) : DbContext(options)
{
    internal IQueryable<DeliveryCompanyReadRow> DeliveryCompanies =>
        Set<DeliveryCompanyReadRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new DeliveryCompanyReadRowConfiguration());
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
