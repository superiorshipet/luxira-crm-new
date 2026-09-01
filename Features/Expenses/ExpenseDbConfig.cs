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
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
    }
}

public class ExchangeRateDbConfig : IDbConfig<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Rate).HasPrecision(18, 4);
    }
}

public class SalesIndicatorDbConfig : IDbConfig<SalesIndicator>
{
    public void Configure(EntityTypeBuilder<SalesIndicator> builder)
    {
        builder.ToTable("SalesIndicators");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TargetAmount).HasPrecision(18, 2);
    }
}
