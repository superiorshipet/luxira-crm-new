namespace Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;

public interface IGetSearchKeywordOptionsRepository
{
    Task<IReadOnlyList<string>> ListCategoriesAsync(
        CancellationToken cancellationToken);
}
