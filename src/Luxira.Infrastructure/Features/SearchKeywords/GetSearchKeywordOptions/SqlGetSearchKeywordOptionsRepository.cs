using Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.Features.SearchKeywords.GetSearchKeywordOptions;

internal sealed class SqlGetSearchKeywordOptionsRepository(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IGetSearchKeywordOptionsRepository
{
    public async Task<IReadOnlyList<string>> ListCategoriesAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        return await context.SearchKeywords
            .Where(keyword => keyword.Category != string.Empty)
            .Select(keyword => keyword.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToArrayAsync(cancellationToken);
    }
}
