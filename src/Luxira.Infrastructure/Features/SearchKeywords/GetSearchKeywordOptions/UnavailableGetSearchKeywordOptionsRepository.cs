using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;

namespace Luxira.Infrastructure.Features.SearchKeywords.GetSearchKeywordOptions;

internal sealed class UnavailableGetSearchKeywordOptionsRepository
    : IGetSearchKeywordOptionsRepository
{
    public Task<IReadOnlyList<string>> ListCategoriesAsync(
        CancellationToken cancellationToken) =>
        throw new ReadStoreUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
