namespace Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;

public sealed class GetSearchKeywordOptionsService(
    IGetSearchKeywordOptionsRepository repository)
{
    public async Task<SearchKeywordOptionsResult> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> categories;
        try
        {
            categories = await repository.ListCategoriesAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            categories = SearchKeywordOptionCatalog.FallbackCategories;
        }

        return new SearchKeywordOptionsResult(
            true,
            categories,
            SearchKeywordOptionCatalog.TargetTypes,
            SearchKeywordOptionCatalog.TypeOptions);
    }
}
