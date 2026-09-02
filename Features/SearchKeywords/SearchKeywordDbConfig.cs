using Luxira.Api.Core;
using Luxira.Api.Features.SearchKeywords.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.SearchKeywords;

public class SearchKeywordDbConfig : IDbConfig<SearchKeywordOption>
{
    public void Configure(EntityTypeBuilder<SearchKeywordOption> builder)
    {
        builder.ToTable("HomeSearchKeywords");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Phrase).HasMaxLength(250).IsRequired();
        builder.Property(s => s.NormalizedPhrase).HasMaxLength(250).IsRequired();
        builder.Property(s => s.TargetType).HasMaxLength(50).IsRequired();
        builder.Property(s => s.TargetValue).HasMaxLength(150).IsRequired();
        builder.Property(s => s.DisplayLabel).HasMaxLength(200);
        builder.Property(s => s.Category).HasMaxLength(100).IsRequired();
        builder.Property(s => s.CreatedBy).HasMaxLength(128);
        builder.Property(s => s.UpdatedBy).HasMaxLength(128);
        builder.Property(s => s.CreatedAt).HasColumnType("datetime");
        builder.Property(s => s.UpdatedAt).HasColumnType("datetime");
    }
}
