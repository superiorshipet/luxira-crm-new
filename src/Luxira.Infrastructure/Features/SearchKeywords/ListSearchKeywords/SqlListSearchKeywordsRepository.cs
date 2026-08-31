using Luxira.Application.Features.SearchKeywords.ListSearchKeywords;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.Features.SearchKeywords.ListSearchKeywords;

internal sealed class SqlListSearchKeywordsRepository(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IListSearchKeywordsRepository
{
    public async Task<IReadOnlyList<SearchKeywordRecord>> ListAsync(
        SearchKeywordFilter filter,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var query = context.SearchKeywords;

        if (filter.Search is not null)
        {
            var pattern = $"%{filter.Search}%";
            query = query.Where(keyword =>
                EF.Functions.Like(keyword.Phrase, pattern) ||
                (keyword.DisplayLabel != null &&
                    EF.Functions.Like(keyword.DisplayLabel, pattern)) ||
                EF.Functions.Like(keyword.Category, pattern) ||
                EF.Functions.Like(keyword.TargetValue, pattern));
        }

        if (filter.TargetType is not null)
        {
            query = query.Where(keyword => keyword.TargetType == filter.TargetType);
        }

        if (filter.Category is not null)
        {
            query = query.Where(keyword => keyword.Category == filter.Category);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(keyword => keyword.IsActive == filter.IsActive.Value);
        }

        return await query
            .OrderByDescending(keyword => keyword.IsActive)
            .ThenByDescending(keyword => keyword.Id)
            .Select(keyword => new SearchKeywordRecord(
                keyword.Id,
                keyword.Phrase,
                keyword.NormalizedPhrase,
                keyword.TargetType,
                keyword.TargetValue,
                keyword.DisplayLabel,
                keyword.Category,
                keyword.IsActive,
                keyword.CreatedAt,
                keyword.CreatedBy,
                keyword.UpdatedAt,
                keyword.UpdatedBy,
                keyword.IsSingleResult))
            .ToArrayAsync(cancellationToken);
    }
}
