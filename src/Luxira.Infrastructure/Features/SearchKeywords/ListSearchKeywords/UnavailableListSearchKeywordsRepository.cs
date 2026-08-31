using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.SearchKeywords.ListSearchKeywords;

namespace Luxira.Infrastructure.Features.SearchKeywords.ListSearchKeywords;

internal sealed class UnavailableListSearchKeywordsRepository
    : IListSearchKeywordsRepository
{
    public Task<IReadOnlyList<SearchKeywordRecord>> ListAsync(
        SearchKeywordFilter filter,
        CancellationToken cancellationToken) =>
        throw new ReadStoreUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
