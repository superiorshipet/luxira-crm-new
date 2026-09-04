using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Features.Operations.Controllers;
using Luxira.Api.Features.Media.Services;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Infrastructure.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;

namespace Luxira.Tests;

public sealed class S3DashboardParityTests
{
    [Fact]
    public async Task OrphanDeletionIsDryRunByDefaultAndDeletesOnlyAfterConfirmation()
    {
        await using var context = CreateContext();
        context.S3StoredObjects.Add(new S3StoredObject { Key = "kept.jpg", Prefix = "test", UploadedAt = DateTime.UtcNow });
        context.OrderPostImages.Add(new OrderPostImage { Id = 9, OrderPostId = 1, Url = "/old.jpg", S3Key = "referenced-without-index.jpg" });
        await context.SaveChangesAsync();
        using var storage = new FakeStorage(context,
        [
            new S3ObjectInfo("kept.jpg", 10, DateTime.UtcNow),
            new S3ObjectInfo("orphan.jpg", 20, DateTime.UtcNow),
            new S3ObjectInfo("referenced-without-index.jpg", 30, DateTime.UtcNow)
        ]);
        var controller = Controller(context, storage);

        await controller.DeleteOrphans(false);
        Assert.Empty(storage.DeletedKeys);

        await controller.DeleteOrphans(true);
        Assert.Equal(["orphan.jpg"], storage.DeletedKeys);
    }

    [Fact]
    public async Task CleanupModePersistsAndRecordsActor()
    {
        await using var context = CreateContext();
        using var storage = new FakeStorage(context, []);
        var controller = Controller(context, storage);

        await controller.SetCleanupDryRun(false, default);

        var setting = await context.MediaReferenceCleanupSettings.SingleAsync();
        Assert.False(setting.DryRun);
        Assert.Equal("admin@example.test", setting.UpdatedBy);
    }

    [Fact]
    public async Task CleanupRunUsesPersistedDryRunAndWritesAuditRow()
    {
        await using var context = CreateContext();
        context.MediaReferenceCleanupSettings.Add(new MediaReferenceCleanupSetting { Id = 1, DryRun = true });
        await context.SaveChangesAsync();
        using var storage = new FakeStorage(context, []);
        var controller = Controller(context, storage);

        var response = Assert.IsType<OkObjectResult>(await controller.RunCleanupNow(default));

        Assert.NotNull(response.Value);
        var run = await context.MediaReferenceCleanupRuns.SingleAsync();
        Assert.True(run.IsDryRun);
        Assert.Equal("admin@example.test", run.TriggeredBy);
    }

    [Fact]
    public async Task ModuleStatusesReturnsEveryLegacyMediaModule()
    {
        await using var context = CreateContext();
        using var storage = new FakeStorage(context, []);
        var environment = new TestWebHostEnvironment();
        var migration = new MediaMigrationService(context, storage, environment, NullLogger<MediaMigrationService>.Instance);

        var statuses = await migration.GetModuleStatusesAsync();

        Assert.Equal(MediaModuleRegistry.Modules.Length, statuses.Count);
        Assert.Contains(statuses, item => item.ModuleKey == MediaModuleRegistry.OrderPostsModuleKey);
        Assert.Contains(statuses, item => item.ModuleKey == "employee-tasks");
    }

    [Fact]
    public async Task OrderPostMigrationPreservesLegacyUrlAndAddsS3Key()
    {
        var root = Path.Combine(Path.GetTempPath(), $"luxira-s3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "images", "orderposts"));
        var physicalPath = Path.Combine(root, "images", "orderposts", "one.jpg");
        await File.WriteAllBytesAsync(physicalPath, [1, 2, 3]);
        try
        {
            await using var context = CreateContext();
            context.OrderPostImages.Add(new OrderPostImage { Id = 1, OrderPostId = 1, Url = "/images/orderposts/one.jpg" });
            await context.SaveChangesAsync();
            using var storage = new FakeStorage(context, []);
            var environment = new TestWebHostEnvironment { WebRootPath = root };
            var migration = new MediaMigrationService(context, storage, environment, NullLogger<MediaMigrationService>.Instance);

            var result = await migration.MigrateBatchAsync(100, 0, "admin-id", "admin");

            Assert.Equal(1, result.Migrated);
            var image = await context.OrderPostImages.SingleAsync();
            Assert.Equal("/images/orderposts/one.jpg", image.Url);
            Assert.Equal("orderposts/test-key.jpg", image.S3Key);
            Assert.Single(context.S3StoredObjects);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static S3DashboardController Controller(ApplicationDbContext context, S3StorageService storage)
    {
        var environment = new TestWebHostEnvironment();
        var migration = new MediaMigrationService(context, storage, environment, NullLogger<MediaMigrationService>.Instance);
        var cleanup = new MediaReferenceCleanupService(context, storage, environment, NullLogger<MediaReferenceCleanupService>.Instance);
        return new S3DashboardController(context, storage, migration, cleanup)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin@example.test")], "test"))
                }
            }
        };
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"s3-dashboard-{Guid.NewGuid():N}")
            .Options);

    private sealed class FakeStorage : S3StorageService
    {
        private readonly ApplicationDbContext _context;
        private readonly IReadOnlyList<S3ObjectInfo> _objects;

        public FakeStorage(ApplicationDbContext context, IReadOnlyList<S3ObjectInfo> objects)
            : base(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:S3:BucketName"] = "test-bucket",
                ["AWS:Region"] = "eu-central-1"
            }).Build(), context, NullLogger<S3StorageService>.Instance)
        {
            _context = context;
            _objects = objects;
        }

        public List<string> DeletedKeys { get; } = [];
        public override Task<IReadOnlyList<S3ObjectInfo>> ListObjectsAsync(string? prefix = null, int maximum = int.MaxValue, CancellationToken ct = default) => Task.FromResult(_objects);
        public override Task DeleteObjectOnlyAsync(string key, CancellationToken ct = default) { DeletedKeys.Add(key); return Task.CompletedTask; }
        public override Task<S3StoredObject> UploadLocalFileAsync(string physicalPath, string prefix, string? originalFileName, string? userId, string? userName, int? orderId = null, bool addToIndex = true, string? explicitKey = null, CancellationToken ct = default)
        {
            var record = new S3StoredObject { Key = explicitKey ?? $"{prefix}/test-key.jpg", Prefix = prefix, OriginalFileName = originalFileName, SizeBytes = new FileInfo(physicalPath).Length, UploadedAt = DateTime.UtcNow, UploadedByUserId = userId, UploadedByUserName = userName };
            if (addToIndex) _context.S3StoredObjects.Add(record);
            return Task.FromResult(record);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Luxira.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
