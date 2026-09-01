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
            monthlyEgressGb = (double?)null,
            region = _s3.Region,
            source = "S3StoredObjects index"
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
        return NotImplemented("Migration progress persistence has not been ported yet.");
    }

    [HttpPost("run-migration")]
    [HttpPost("/S3Dashboard/RunMigration")]
    public IActionResult RunMigration([FromQuery] int batchSize = 100, [FromQuery] int afterId = 0)
    {
        return NotImplemented("S3 migration execution is disabled until its idempotent cursor and audit log are ported.");
    }

    [HttpPost("disk-usage")]
    [HttpPost("/S3Dashboard/DiskUsage")]
    public IActionResult DiskUsage()
    {
        return NotImplemented("Filesystem usage collection is not configured for this deployment.");
    }

    [HttpPost("reconcile")]
    [HttpPost("/S3Dashboard/Reconcile")]
    public IActionResult Reconcile()
    {
        return NotImplemented("S3 reconciliation is not available until a durable reconciliation run model is ported.");
    }

    [HttpPost("repair-index")]
    [HttpPost("/S3Dashboard/RepairIndex")]
    public IActionResult RepairIndex()
    {
        return NotImplemented("S3 index repair is not available yet.");
    }

    [HttpPost("delete-orphans")]
    [HttpPost("/S3Dashboard/DeleteOrphans")]
    public IActionResult DeleteOrphans([FromQuery] bool confirm = false)
    {
        return NotImplemented("Orphan deletion is disabled until reconciliation and recovery guarantees are implemented.");
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
        return NotImplemented("Module migration is not available yet.");
    }

    [HttpPost("module-delete-local")]
    [HttpPost("/S3Dashboard/ModuleDeleteLocal")]
    public IActionResult ModuleDeleteLocal([FromBody] object request)
    {
        return NotImplemented("Local-file deletion is disabled until migration verification is implemented.");
    }

    private ObjectResult NotImplemented(string detail) => StatusCode(
        StatusCodes.Status501NotImplemented,
        new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Operation not implemented",
            Detail = detail
        });

    private Task<int> CountIndexedPrefixAsync(string prefix, CancellationToken ct) =>
        _context.S3StoredObjects.AsNoTracking().CountAsync(item => item.S3Key.StartsWith(prefix), ct);
}
