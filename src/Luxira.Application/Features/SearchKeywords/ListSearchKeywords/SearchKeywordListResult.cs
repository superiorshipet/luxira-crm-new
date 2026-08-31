namespace Luxira.Application.Features.SearchKeywords.ListSearchKeywords;

public sealed record SearchKeywordListResult(
    bool Ok,
    IReadOnlyList<SearchKeywordRecord> Keywords);
