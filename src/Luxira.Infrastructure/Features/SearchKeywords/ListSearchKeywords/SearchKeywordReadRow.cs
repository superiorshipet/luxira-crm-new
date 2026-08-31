namespace Luxira.Infrastructure.Features.SearchKeywords.ListSearchKeywords;

internal sealed class SearchKeywordReadRow
{
    internal int Id { get; init; }
    internal required string Phrase { get; init; }
    internal required string NormalizedPhrase { get; init; }
    internal required string TargetType { get; init; }
    internal required string TargetValue { get; init; }
    internal string? DisplayLabel { get; init; }
    internal required string Category { get; init; }
    internal bool IsActive { get; init; }
    internal bool IsSingleResult { get; init; }
    internal DateTime CreatedAt { get; init; }
    internal string? CreatedBy { get; init; }
    internal DateTime? UpdatedAt { get; init; }
    internal string? UpdatedBy { get; init; }
}
