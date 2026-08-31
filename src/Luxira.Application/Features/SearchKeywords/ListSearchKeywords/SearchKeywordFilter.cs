namespace Luxira.Application.Features.SearchKeywords.ListSearchKeywords;

public sealed record SearchKeywordFilter(
    string? Search,
    string? TargetType,
    string? Category,
    bool? IsActive);
