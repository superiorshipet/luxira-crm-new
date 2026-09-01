namespace Luxira.Api.Features.SearchKeywords.DTOs;

public record SearchKeywordRecord(
    int Id,
    string Keyword,
    string? TargetType,
    string? Category,
    string? TargetValue,
    bool IsActive,
    int SortOrder
);

public record SearchKeywordListResult(
    IReadOnlyList<SearchKeywordRecord> Items,
    int TotalCount
);

public record SearchKeywordOptionDto(
    int Id,
    string Name,
    string? Value,
    string? Type
);

public record SearchKeywordOptionsResult(
    IReadOnlyList<SearchKeywordOptionDto> TargetTypes,
    IReadOnlyList<SearchKeywordOptionDto> Categories
);

public record CreateSearchKeywordRequest(
    string Keyword,
    string? TargetType,
    string? Category,
    string? TargetValue,
    int SortOrder = 0
);
