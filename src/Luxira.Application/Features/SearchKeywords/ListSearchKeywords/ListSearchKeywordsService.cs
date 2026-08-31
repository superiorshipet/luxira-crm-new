namespace Luxira.Application.Features.SearchKeywords.ListSearchKeywords;

public sealed class ListSearchKeywordsService(
    IListSearchKeywordsRepository repository)
{
    public async Task<SearchKeywordListResult> ExecuteAsync(
        string? search,
        string? targetType,
        string? category,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var filter = new SearchKeywordFilter(
            NormalizeOptional(search),
            NormalizeSelectFilter(targetType),
            NormalizeSelectFilter(category),
            isActive);
        var keywords = await repository.ListAsync(filter, cancellationToken);
        return new SearchKeywordListResult(true, keywords);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSelectFilter(string? value)
    {
        var normalized = NormalizeOptional(value);
        return normalized == "All" ? null : normalized;
    }
}
