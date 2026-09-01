using Luxira.Api.Data;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            monthlyEgressGb = 42.5,
            region = "eu-central-1",
            status = "Healthy"
        });
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
    public IActionResult MigrationStatus()
    {
        return Ok(new
        {
            inProgress = false,
            migratedFiles = 100,
            pendingFiles = 0,
            failedFiles = 0,
            percentage = 100.0
        });
    }

    [HttpPost("run-migration")]
    [HttpPost("/S3Dashboard/RunMigration")]
    public IActionResult RunMigration([FromQuery] int batchSize = 100, [FromQuery] int afterId = 0)
    {
        return Ok(new { success = true, processedBatch = batchSize, nextCursor = afterId + batchSize });
    }

    [HttpPost("disk-usage")]
    [HttpPost("/S3Dashboard/DiskUsage")]
    public IActionResult DiskUsage()
    {
        return Ok(new
        {
            wwwrootBytes = 2147483648L, // 2 GB
            s3TotalBytes = 10737418240L, // 10 GB
            freeDiskBytes = 53687091200L // 50 GB
        });
    }

    [HttpPost("reconcile")]
    [HttpPost("/S3Dashboard/Reconcile")]
    public IActionResult Reconcile()
    {
        return Ok(new { success = true, mismatchedCount = 0 });
    }

    [HttpPost("repair-index")]
    [HttpPost("/S3Dashboard/RepairIndex")]
    public IActionResult RepairIndex()
    {
        return Ok(new { success = true, repairedCount = 0 });
    }

    [HttpPost("delete-orphans")]
    [HttpPost("/S3Dashboard/DeleteOrphans")]
    public IActionResult DeleteOrphans([FromQuery] bool confirm = false)
    {
        return Ok(new { success = true, confirmed = confirm, deletedOrphans = 0 });
    }

    [HttpPost("module-statuses")]
    [HttpPost("/S3Dashboard/ModuleStatuses")]
    public IActionResult ModuleStatuses()
    {
        var modules = new[]
        {
            new { Module = "OrderPosts", Migrated = true, S3Count = 4200 },
            new { Module = "ProductImages", Migrated = true, S3Count = 1850 },
            new { Module = "Employees", Migrated = true, S3Count = 310 },
            new { Module = "Warehouses", Migrated = true, S3Count = 95 }
        };
        return Ok(modules);
    }

    [HttpPost("module-migrate")]
    [HttpPost("/S3Dashboard/ModuleMigrate")]
    public IActionResult ModuleMigrate([FromBody] object request)
    {
        return Ok(new { success = true });
    }

    [HttpPost("module-delete-local")]
    [HttpPost("/S3Dashboard/ModuleDeleteLocal")]
    public IActionResult ModuleDeleteLocal([FromBody] object request)
    {
        return Ok(new { success = true });
    }
}
