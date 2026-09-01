namespace Luxira.Api.Features.SearchKeywords.Models;

public class SearchKeywordOption
{
    public int Id { get; set; }
    public string Phrase { get; set; } = string.Empty;
    public string NormalizedPhrase { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public string? DisplayLabel { get; set; }
    public string Category { get; set; } = "عام";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsSingleResult { get; set; }
}
