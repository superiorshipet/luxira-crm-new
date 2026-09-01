using Luxira.Api.Core;
using Luxira.Api.Features.Expenses.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Expenses;

public class ExpenseDbConfig : IDbConfig<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Description).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
    }
}

public class ExchangeRateDbConfig : IDbConfig<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.BuyToUSD).HasPrecision(18, 4);
        builder.Property(r => r.SellToUSD).HasPrecision(18, 4);
    }
}

public class SalesIndicatorDbConfig : IDbConfig<SalesIndicator>
{
    public void Configure(EntityTypeBuilder<SalesIndicator> builder)
    {
        builder.ToTable("SalesIndicators");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.MinimumSellingFrom).HasPrecision(18, 2);
        builder.Property(s => s.MinimumSellingTo).HasPrecision(18, 2);
        builder.Property(s => s.BasicSellingFrom).HasPrecision(18, 2);
        builder.Property(s => s.BasicSellingTo).HasPrecision(18, 2);
        builder.Property(s => s.MiddleSellingFrom).HasPrecision(18, 2);
        builder.Property(s => s.MiddleSellingTo).HasPrecision(18, 2);
    }
}
