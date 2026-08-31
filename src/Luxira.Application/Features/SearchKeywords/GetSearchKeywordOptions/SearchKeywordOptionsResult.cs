namespace Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;

public sealed record SearchKeywordOptionsResult(
    bool Ok,
    IReadOnlyList<string> Categories,
    IReadOnlyList<SearchKeywordOption> TargetTypes,
    IReadOnlyDictionary<string, IReadOnlyList<SearchKeywordOption>> TypeOptions);
