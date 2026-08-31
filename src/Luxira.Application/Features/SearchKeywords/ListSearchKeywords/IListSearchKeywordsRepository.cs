namespace Luxira.Application.Features.SearchKeywords.ListSearchKeywords;

public interface IListSearchKeywordsRepository
{
    Task<IReadOnlyList<SearchKeywordRecord>> ListAsync(
        SearchKeywordFilter filter,
        CancellationToken cancellationToken);
}
