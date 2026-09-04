using Luxira.Api.Data;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Luxira.Api.Features.SearchKeywords.Services;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Features.Media.Services;
using System.Collections.Concurrent;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator")]
[Route("api/v1/operations/s3")]
[Route("S3Dashboard")]
public class S3DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _s3;
    private readonly MediaMigrationService _migration;
    private readonly MediaReferenceCleanupService _cleanup;

    public S3DashboardController(ApplicationDbContext context, S3StorageService s3, MediaMigrationService migration, MediaReferenceCleanupService cleanup)
    {
        _context = context;
        _s3 = s3;
        _migration = migration;
        _cleanup = cleanup;
    }

    [HttpGet("metrics")]
    [HttpGet("Index")]
    [HttpGet("/S3Dashboard/Index")]
    [HttpPost("/S3Dashboard/Index")]
    [HttpGet("/S3Dashboard/GetMetrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken ct)
    {
        var (totalBytes, count) = await _s3.GetBucketMetricsAsync(ct);
        double gb = Math.Round((double)totalBytes / (1024 * 1024 * 1024), 2);

        return Ok(new
        {
            bucketName = _s3.BucketName,
            storageBytes = totalBytes,
            storageFormatted = $"{gb} GB",
            objectCount = count,
            monthlyEgressGb = (double?)null,
            region = _s3.Region,
            source = "S3StoredObjects index"
        });
    }

    [HttpPost("/S3Dashboard/BulkUpload")]
    [RequestSizeLimit(2L * 1024 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024)]
    public async Task<IActionResult> BulkUpload([FromForm] List<IFormFile> files, [FromForm] string? prefix, CancellationToken ct)
    {
        if (files.Count == 0) return BadRequest(new { message = "لم يتم اختيار أي ملف." });
        var succeeded = new List<object>(); var failed = new List<object>();
        foreach (var file in files)
        {
            try
            {
                var stored = await _s3.UploadAsync(file, string.IsNullOrWhiteSpace(prefix) ? "misc" : prefix.Trim('/'), User.GetUserId(), ct);
                succeeded.Add(new { key = stored.Key, name = stored.OriginalFileName, size = stored.SizeBytes });
            }
            catch (Exception ex) { failed.Add(new { name = file.FileName, error = ex.Message }); }
        }
        return Ok(new { uploaded = succeeded.Count, failedCount = failed.Count, succeeded, failed });
    }

    [HttpPost("recalculate")]
    [HttpPost("/S3Dashboard/RecalculateStorage")]
    public async Task<IActionResult> RecalculateStorage(CancellationToken ct)
    {
        var objects = await _s3.ListObjectsAsync(ct: ct);
        var totalBytes = objects.Sum(item => item.Size);
        var indexedObjectCount = await _context.S3StoredObjects.AsNoTracking().CountAsync(item => !item.IsDeleted, ct);
        return Ok(new { totalBytes, totalGb = Math.Round((double)totalBytes / (1024 * 1024 * 1024), 3), objectCount = objects.Count, indexedObjectCount });
    }

    [HttpPost("migration-status")]
    [HttpPost("/S3Dashboard/MigrationStatus")]
    public async Task<IActionResult> MigrationStatus(CancellationToken ct)
    {
        var status = await _migration.GetStatusAsync(ct);
        return Ok(new
        {
            totalImages = status.TotalImages,
            migratedCount = status.MigratedCount,
            pendingCount = status.PendingCount,
            readyCount = status.ReadyCount,
            readyBytes = status.ReadyBytes,
            missingFileCount = status.MissingFileCount,
            notLocalCount = status.NotLocalCount,
            isEstimateCapped = status.IsEstimateCapped
        });
    }

    [HttpPost("run-migration")]
    [HttpPost("/S3Dashboard/RunMigration")]
    public async Task<IActionResult> RunMigration([FromQuery] int batchSize = 100, [FromQuery] int afterId = 0, CancellationToken ct = default)
    {
        var result = await _migration.MigrateBatchAsync(batchSize, afterId, User.GetUserId(), User.Identity?.Name, ct);
        return Ok(new
        {
            result.Examined,
            result.Migrated,
            result.MigratedBytes,
            result.SkippedMissingFile,
            result.SkippedNotLocal,
            result.FailedCount,
            result.Errors,
            nextAfterId = result.LastProcessedId,
            completed = !result.HasMore
        });
    }

    [HttpPost("disk-usage")]
    [HttpPost("/S3Dashboard/DiskUsage")]
    public IActionResult DiskUsage([FromServices] IWebHostEnvironment environment)
    {
        var files = Directory.Exists(environment.WebRootPath) ? Directory.EnumerateFiles(environment.WebRootPath, "*", SearchOption.AllDirectories) : [];
        long bytes = 0; var count = 0;
        foreach (var file in files) { try { bytes += new FileInfo(file).Length; count++; } catch { } }
        return Ok(new { fileCount = count, totalBytes = bytes });
    }

    [HttpPost("reconcile")]
    [HttpPost("/S3Dashboard/Reconcile")]
    public async Task<IActionResult> Reconcile(CancellationToken ct)
    {
        var bucketObjects = await _s3.ListObjectsAsync(ct: ct);
        var bucket = bucketObjects.GroupBy(item => item.Key, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Size, StringComparer.Ordinal);
        var indexRows = await _context.S3StoredObjects.AsNoTracking().Where(item => !item.IsDeleted).Select(item => new { item.Key, item.SizeBytes }).ToListAsync(ct);
        var indexed = indexRows.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var orphans = bucket.Keys.Where(key => !indexed.Contains(key)).ToList();
        var missing = indexRows.Where(item => !bucket.ContainsKey(item.Key)).ToList();
        return Ok(new
        {
            bucketObjectCount = bucket.Count,
            indexedObjectCount = indexRows.Count,
            orphanCount = orphans.Count,
            orphanBytes = orphans.Sum(key => bucket[key]),
            orphanSample = orphans.Take(50),
            missingCount = missing.Count,
            missingSample = missing.Take(50).Select(item => item.Key)
        });
    }

    [HttpPost("repair-index")]
    [HttpPost("/S3Dashboard/RepairIndex")]
    public async Task<IActionResult> RepairIndex(CancellationToken ct)
    {
        var result = await _migration.RepairIndexAsync(User.GetUserId(), User.Identity?.Name, ct);
        return Ok(new
        {
            result.ReferencedKeyCount,
            result.MissingFromIndexCount,
            result.RepairedCount,
            result.RepairedBytes,
            result.NotInBucketCount,
            result.NotInBucketSample,
            result.FailedCount,
            result.Errors
        });
    }

    [HttpPost("delete-orphans")]
    [HttpPost("/S3Dashboard/DeleteOrphans")]
    public async Task<IActionResult> DeleteOrphans([FromQuery] bool confirm = false, CancellationToken ct = default)
    {
        var bucketObjects = await _s3.ListObjectsAsync(ct: ct);
        var indexedKeys = await _context.S3StoredObjects.AsNoTracking().Where(item => !item.IsDeleted).Select(item => item.Key).ToHashSetAsync(ct);
        var referencedKeys = await _context.OrderPostImages.AsNoTracking().Where(item => item.S3Key != null).Select(item => item.S3Key!).Distinct().ToHashSetAsync(ct);
        var unindexed = bucketObjects.Where(item => !indexedKeys.Contains(item.Key)).ToList();
        var stillReferenced = unindexed.Where(item => referencedKeys.Contains(item.Key)).ToList();
        var candidates = unindexed.Where(item => !referencedKeys.Contains(item.Key)).ToList();
        var deleted = new ConcurrentBag<S3ObjectInfo>();
        var errors = new ConcurrentBag<object>();

        if (confirm)
        {
            await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, async (item, token) =>
            {
                try { await _s3.DeleteObjectOnlyAsync(item.Key, token); deleted.Add(item); }
                catch (Exception exception) { errors.Add(new { item.Key, error = exception.Message }); }
            });
        }

        return Ok(new
        {
            wasDryRun = !confirm,
            deletableCount = candidates.Count,
            deletableBytes = candidates.Sum(item => item.Size),
            deletedCount = deleted.Count,
            deletedBytes = deleted.Sum(item => item.Size),
            deletedSample = candidates.Take(50).Select(item => item.Key),
            stillReferencedCount = stillReferenced.Count,
            stillReferencedBytes = stillReferenced.Sum(item => item.Size),
            stillReferencedSample = stillReferenced.Take(50).Select(item => item.Key),
            failedCount = errors.Count,
            errors
        });
    }

    [HttpPost("module-statuses")]
    [HttpPost("/S3Dashboard/ModuleStatuses")]
    public async Task<IActionResult> ModuleStatuses(CancellationToken ct)
    {
        var statuses = await _migration.GetModuleStatusesAsync(ct);
        return Ok(new { modules = statuses.Select(item => new { key = item.ModuleKey, item.Label, item.Note, item.Total, item.Migrated, item.Pending }) });
    }

    [HttpPost("module-migrate")]
    [HttpPost("/S3Dashboard/ModuleMigrate")]
    public async Task<IActionResult> ModuleMigrate([FromBody] ModuleBatchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleKey)) return BadRequest(new { message = "الوحدة مطلوبة." });
        try
        {
            var result = await _migration.MigrateModuleBatchAsync(request.ModuleKey, request.BatchSize <= 0 ? 100 : request.BatchSize, request.Cursors, User.GetUserId(), User.Identity?.Name, ct);
            return Ok(result);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("module-delete-local")]
    [HttpPost("/S3Dashboard/ModuleDeleteLocal")]
    public async Task<IActionResult> ModuleDeleteLocal([FromBody] ModuleDeleteLocalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleKey)) return BadRequest(new { message = "الوحدة مطلوبة." });
        try
        {
            var result = await _migration.DeleteLocalModuleBatchAsync(request.ModuleKey, request.BatchSize <= 0 ? 100 : request.BatchSize, request.Cursors, request.Confirm, ct);
            return Ok(result);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("/S3Dashboard/RunCleanupNow")]
    public async Task<IActionResult> RunCleanupNow(CancellationToken ct)
    {
        var run = await _cleanup.RunAsync(User.Identity?.Name ?? "admin", ct);
        return Ok(new
        {
            run.Id,
            run.IsDryRun,
            run.RowsScanned,
            run.WouldClearCount,
            run.ReferencesCleared,
            run.SkippedStillInBucket,
            run.FailedCount,
            run.WasAborted,
            run.AbortReason,
            run.ScanWasCapped,
            run.DurationMs,
            run.Error
        });
    }

    [HttpPost("/S3Dashboard/SetCleanupDryRun")]
    public async Task<IActionResult> SetCleanupDryRun([FromForm] bool dryRun, CancellationToken ct)
    {
        var setting = await _cleanup.SetDryRunAsync(dryRun, User.Identity?.Name, ct);
        return Ok(new
        {
            setting.DryRun,
            setting.UpdatedBy,
            message = setting.DryRun
                ? "وضع المعاينة مُفعّل — لن يتم مسح أي مرجع."
                : "وضع المسح الفعلي مُفعّل — سيتم مسح المراجع الميتة."
        });
    }

    [HttpPost("/S3Dashboard/PresignedUrl")]
    public IActionResult PresignedUrl([FromForm] string key) => string.IsNullOrWhiteSpace(key) ? BadRequest(new { message = "المفتاح مطلوب." }) : Ok(new { url = _s3.GetPresignedUrl(key) });

    [HttpPost("/S3Dashboard/Delete")]
    public async Task<IActionResult> Delete([FromForm] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(new { message = "المفتاح مطلوب." });
        await _s3.DeleteAsync(key, User.GetUserId(), ct);
        return Ok(new { message = "تم الحذف." });
    }

    [HttpPost("/S3Dashboard/ComputeImageHashes")]
    public async Task<IActionResult> ComputeImageHashes([FromServices] ImageSearchService imageSearch, CancellationToken ct)
    {
        var images = await _context.OrderPostImages.Where(image => image.PHash == null && image.S3Key != null).OrderBy(image => image.Id).Take(50).ToListAsync(ct);
        var processed = 0; var failed = 0;
        foreach (var image in images)
        {
            try
            {
                var (bytes, _) = await _s3.DownloadAsync(image.S3Key!, ct);
                await using var stream = new MemoryStream(bytes, writable: false);
                image.PHash = await ImageSearchService.ComputeHashAsync(stream, ct);
                if (image.PHash.HasValue) processed++; else failed++;
            }
            catch { failed++; }
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { processed, failed, remaining = images.Count == 50 });
    }

    public class ModuleBatchRequest
    {
        public string? ModuleKey { get; set; }
        public int BatchSize { get; set; }
        public Dictionary<string, int>? Cursors { get; set; }
    }

    public sealed class ModuleDeleteLocalRequest : ModuleBatchRequest
    {
        public bool Confirm { get; set; }
    }
}
