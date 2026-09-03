namespace Luxira.Api.Features.Operations.Models;

public class AppLog
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? Type { get; set; }
    public string? Kind { get; set; }
}

public class AppMetric
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Kind { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public string? Path { get; set; }
    public string? UserName { get; set; }
    public int? Serial { get; set; }
    public string? Label { get; set; }
    public int? SqlCount { get; set; }
    public double? SqlTotalMs { get; set; }
    public int? RowCount { get; set; }
    public string? MetricsJson { get; set; }
    public string? Detail { get; set; }
}
