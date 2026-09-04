namespace Luxira.Api.Features.Media.Models;

public sealed class MediaMigrationStatus
{
    public int TotalImages { get; set; }
    public int MigratedCount { get; set; }
    public int PendingCount { get; set; }
    public int ReadyCount { get; set; }
    public long ReadyBytes { get; set; }
    public int MissingFileCount { get; set; }
    public int NotLocalCount { get; set; }
    public bool IsEstimateCapped { get; set; }
}

public sealed class MediaMigrationBatchResult
{
    public int Examined { get; set; }
    public int Migrated { get; set; }
    public long MigratedBytes { get; set; }
    public int SkippedMissingFile { get; set; }
    public int SkippedNotLocal { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public int LastProcessedId { get; set; }
    public bool HasMore { get; set; }
}

public sealed class IndexRepairResult
{
    public int ReferencedKeyCount { get; set; }
    public int MissingFromIndexCount { get; set; }
    public int RepairedCount { get; set; }
    public long RepairedBytes { get; set; }
    public int NotInBucketCount { get; set; }
    public List<string> NotInBucketSample { get; set; } = [];
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class MediaModuleStatus
{
    public string ModuleKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int Total { get; set; }
    public int Migrated { get; set; }
    public int Pending { get; set; }
}

public sealed class ModuleBatchResult
{
    public string ModuleKey { get; set; } = string.Empty;
    public int Examined { get; set; }
    public int Migrated { get; set; }
    public long MigratedBytes { get; set; }
    public int SkippedMissingFile { get; set; }
    public int SkippedNotLocal { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public Dictionary<string, int> Cursors { get; set; } = [];
    public bool HasMore { get; set; }
}

public sealed class LocalDeleteBatchResult
{
    public string ModuleKey { get; set; } = string.Empty;
    public bool WasDryRun { get; set; }
    public int Examined { get; set; }
    public int Deletable { get; set; }
    public long DeletableBytes { get; set; }
    public int Deleted { get; set; }
    public long DeletedBytes { get; set; }
    public int AlreadyGone { get; set; }
    public int KeptNotInBucket { get; set; }
    public List<string> KeptNotInBucketSample { get; set; } = [];
    public int KeptSizeMismatch { get; set; }
    public List<string> KeptSizeMismatchSample { get; set; } = [];
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public Dictionary<string, int> Cursors { get; set; } = [];
    public bool HasMore { get; set; }
}
