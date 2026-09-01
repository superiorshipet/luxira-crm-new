using Luxira.Api.Core;
using Luxira.Api.Features.SearchKeywords.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.SearchKeywords;

public class SearchKeywordDbConfig : IDbConfig<SearchKeywordOption>
{
    public void Configure(EntityTypeBuilder<SearchKeywordOption> builder)
    {
        builder.ToTable("SearchKeywordOptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Keyword).HasMaxLength(150).IsRequired();
        builder.Property(s => s.TargetType).HasMaxLength(50);
        builder.Property(s => s.Category).HasMaxLength(100);
    }
}
