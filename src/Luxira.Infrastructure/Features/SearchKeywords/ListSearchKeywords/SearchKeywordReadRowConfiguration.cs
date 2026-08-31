using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Infrastructure.Features.SearchKeywords.ListSearchKeywords;

internal sealed class SearchKeywordReadRowConfiguration
    : IEntityTypeConfiguration<SearchKeywordReadRow>
{
    public void Configure(EntityTypeBuilder<SearchKeywordReadRow> builder)
    {
        builder.ToTable("HomeSearchKeywords");
        builder.HasKey(keyword => keyword.Id);
        builder.Property(keyword => keyword.Id).ValueGeneratedOnAdd();
        builder.Property(keyword => keyword.Phrase).IsRequired().HasMaxLength(250);
        builder.Property(keyword => keyword.NormalizedPhrase)
            .IsRequired()
            .HasMaxLength(250);
        builder.Property(keyword => keyword.TargetType)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(keyword => keyword.TargetValue)
            .IsRequired()
            .HasMaxLength(150);
        builder.Property(keyword => keyword.DisplayLabel).HasMaxLength(200);
        builder.Property(keyword => keyword.Category)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(keyword => keyword.CreatedBy).HasMaxLength(128);
        builder.Property(keyword => keyword.UpdatedBy).HasMaxLength(128);
    }
}
