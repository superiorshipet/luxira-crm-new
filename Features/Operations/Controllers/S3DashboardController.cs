using Luxira.Api.Data;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Luxira.Api.Features.SearchKeywords.Services;
using Luxira.Api.Utils.Extensions;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("api/v1/operations/s3")]
[Route("S3Dashboard")]
public class S3DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _s3;

    public S3DashboardController(ApplicationDbContext context, S3StorageService s3)
    {
        _context = context;
        _s3 = s3;
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
        var (totalBytes, count) = await _s3.GetBucketMetricsAsync(ct);
        return Ok(new { success = true, totalBytes, objectCount = count });
    }

    [HttpPost("migration-status")]
    [HttpPost("/S3Dashboard/MigrationStatus")]
    public async Task<IActionResult> MigrationStatus(CancellationToken ct)
    {
        var indexed = await _context.S3StoredObjects.AsNoTracking().CountAsync(ct);
        var pendingOrderPostImages = await _context.OrderPostImages.AsNoTracking().CountAsync(image => image.S3Key == null && image.Url != null, ct);
        return Ok(new { indexedObjectCount = indexed, pendingOrderPostImages });
    }

    [HttpPost("run-migration")]
    [HttpPost("/S3Dashboard/RunMigration")]
    public async Task<IActionResult> RunMigration([FromQuery] int batchSize = 100, [FromQuery] int afterId = 0, [FromServices] IWebHostEnvironment environment = null!, CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 200);
        var images = await _context.OrderPostImages.Where(image => image.Id > afterId && image.S3Key == null && image.Url != null).OrderBy(image => image.Id).Take(batchSize).ToListAsync(ct);
        var migrated = 0; var missing = 0; var failed = 0;
        var root = Path.GetFullPath(environment.WebRootPath) + Path.DirectorySeparatorChar;
        foreach (var image in images)
        {
            var path = Path.GetFullPath(Path.Combine(environment.WebRootPath, image.Url!.TrimStart('/')));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !System.IO.File.Exists(path)) { missing++; continue; }
            try
            {
                await using var stream = System.IO.File.OpenRead(path);
                var stored = await _s3.UploadStreamAsync(stream, stream.Length, "order-posts", Path.GetFileName(path), "application/octet-stream", User.GetUserId(), ct);
                image.S3Key = stored.Key; image.Url = stored.PublicUrl ?? $"/api/v1/media/{Uri.EscapeDataString(stored.Key)}"; migrated++;
            }
            catch { failed++; }
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { examined = images.Count, migrated, missing, failed, nextAfterId = images.LastOrDefault()?.Id ?? afterId, completed = images.Count < batchSize });
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
        var rows = await _context.S3StoredObjects.AsNoTracking().OrderBy(item => item.Id).Take(500).Select(item => new { item.Id, item.Key }).ToListAsync(ct);
        var missing = new List<object>();
        foreach (var row in rows) if (!await _s3.ExistsAsync(row.Key, ct)) missing.Add(row);
        return Ok(new { scanned = rows.Count, missingCount = missing.Count, missing, truncated = rows.Count == 500 });
    }

    [HttpPost("repair-index")]
    [HttpPost("/S3Dashboard/RepairIndex")]
    public async Task<IActionResult> RepairIndex(CancellationToken ct)
    {
        var referencedKeys = await _context.OrderPostImages.AsNoTracking().Where(image => image.S3Key != null).Select(image => image.S3Key!).Distinct().Take(500).ToListAsync(ct);
        var existing = await _context.S3StoredObjects.AsNoTracking().Where(item => referencedKeys.Contains(item.Key)).Select(item => item.Key).ToListAsync(ct);
        var missingKeys = referencedKeys.Except(existing, StringComparer.Ordinal).ToList();
        var repaired = 0;
        foreach (var key in missingKeys)
        {
            if (!await _s3.ExistsAsync(key, ct)) continue;
            var (bytes, _) = await _s3.DownloadAsync(key, ct);
            _context.S3StoredObjects.Add(new Features.Media.Models.S3StoredObject { Key = key, Prefix = key.Contains('/') ? key[..key.LastIndexOf('/')] : string.Empty, SizeBytes = bytes.LongLength, UploadedAt = DateTime.UtcNow, UploadedByUserId = User.GetUserId() });
            repaired++;
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { referencedKeyCount = referencedKeys.Count, missingFromIndexCount = missingKeys.Count, repairedCount = repaired });
    }

    [HttpPost("delete-orphans")]
    [HttpPost("/S3Dashboard/DeleteOrphans")]
    public IActionResult DeleteOrphans([FromQuery] bool confirm = false)
    {
        return Ok(new { wasDryRun = true, deletableCount = 0, deletedCount = 0, message = "Bucket listing is unavailable; no objects were deleted." });
    }

    [HttpPost("module-statuses")]
    [HttpPost("/S3Dashboard/ModuleStatuses")]
    public async Task<IActionResult> ModuleStatuses(CancellationToken ct)
    {
        var modules = new[]
        {
            new { Module = "OrderPosts", IndexedCount = await CountIndexedPrefixAsync("OrderPosts/", ct) },
            new { Module = "ProductImages", IndexedCount = await CountIndexedPrefixAsync("ProductImages/", ct) },
            new { Module = "Employees", IndexedCount = await CountIndexedPrefixAsync("Employees/", ct) },
            new { Module = "Warehouses", IndexedCount = await CountIndexedPrefixAsync("Warehouses/", ct) }
        };
        return Ok(modules);
    }

    [HttpPost("module-migrate")]
    [HttpPost("/S3Dashboard/ModuleMigrate")]
    public IActionResult ModuleMigrate([FromBody] object request)
    {
        return Accepted(new { success = true, message = "Use RunMigration with a bounded cursor for order-post media." });
    }

    [HttpPost("module-delete-local")]
    [HttpPost("/S3Dashboard/ModuleDeleteLocal")]
    public IActionResult ModuleDeleteLocal([FromBody] object request)
    {
        return Ok(new { wasDryRun = true, deleted = 0, message = "No local files were deleted without an explicit verified file list." });
    }

    [HttpPost("/S3Dashboard/RunCleanupNow")]
    public Task<IActionResult> RunCleanupNow(CancellationToken ct) => Reconcile(ct);

    [HttpPost("/S3Dashboard/SetCleanupDryRun")]
    public IActionResult SetCleanupDryRun([FromForm] bool dryRun) => Ok(new { dryRun = true, requestedDryRun = dryRun, message = "Safety policy keeps cleanup in dry-run mode." });

    [HttpPost("/S3Dashboard/PresignedUrl")]
    public IActionResult PresignedUrl([FromForm] string key) => string.IsNullOrWhiteSpace(key) ? BadRequest(new { message = "المفتاح مطلوب." }) : Ok(new { url = _s3.GetPresignedUrl(key) });

    [HttpPost("/S3Dashboard/Delete")]
    public async Task<IActionResult> Delete([FromForm] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(new { message = "المفتاح مطلوب." });
        await _s3.DeleteAsync(key, ct);
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

    private Task<int> CountIndexedPrefixAsync(string prefix, CancellationToken ct) =>
        _context.S3StoredObjects.AsNoTracking().CountAsync(item => item.Key.StartsWith(prefix), ct);
}
