namespace Luxira.Api.Features.Media.Models;

public sealed class MediaReferenceCleanupSetting
{
    public int Id { get; set; }
    public bool DryRun { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class MediaReferenceCleanupRun
{
    public int Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public string TriggeredBy { get; set; } = "auto";
    public bool IsDryRun { get; set; }
    public int RowsScanned { get; set; }
    public int WouldClearCount { get; set; }
    public int ReferencesCleared { get; set; }
    public int SkippedStillInBucket { get; set; }
    public int FailedCount { get; set; }
    public bool WasAborted { get; set; }
    public string? AbortReason { get; set; }
    public string? Error { get; set; }
    public string? ClearedEntriesJson { get; set; }
    public string? CursorsJson { get; set; }
    public bool ScanWasCapped { get; set; }
}
