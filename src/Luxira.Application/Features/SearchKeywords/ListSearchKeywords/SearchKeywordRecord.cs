namespace Luxira.Application.Features.SearchKeywords.ListSearchKeywords;

public sealed record SearchKeywordRecord(
    int Id,
    string Phrase,
    string NormalizedPhrase,
    string TargetType,
    string TargetValue,
    string? DisplayLabel,
    string Category,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    bool IsSingleResult);
