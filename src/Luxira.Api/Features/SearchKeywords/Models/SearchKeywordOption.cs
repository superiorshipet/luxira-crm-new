namespace Luxira.Api.Features.SearchKeywords.Models;

public class SearchKeywordOption
{
    public int Id { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? Category { get; set; }
    public string? TargetValue { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
