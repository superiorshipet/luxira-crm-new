using System.Reflection;
using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Time;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Media.Services;
    /// <summary>
    /// Backfills order-post images from wwwroot into the S3 media bucket.
    ///
    /// Additive on purpose: it copies the file up and writes OrderPostImage.S3Key, and touches
    /// nothing else. The Url column keeps its original wwwroot path and the file on disk is left
    /// alone, so a migrated row still has a complete, working local reference behind it. Undoing
    /// the switch means ignoring S3Key — there is nothing to restore.
    ///
    /// Only pre-existing media is in scope. Images uploaded after the switch go straight to S3 and
    /// are born with a key, so the backfill never sees them.
    /// </summary>
    public class MediaMigrationService
    {
        /// <summary>
        /// How many pending rows the status scan will stat before giving up on an exact byte
        /// figure. Each one is a filesystem hit, and the scan runs on a button press in a request —
        /// the counts stay exact regardless, only ReadyBytes is capped.
        /// </summary>
        private const int StatScanCap = 25_000;

        private readonly ApplicationDbContext _db;
        private readonly S3StorageService _storage;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<MediaMigrationService> _logger;

        public MediaMigrationService(
            ApplicationDbContext db,
            S3StorageService storage,
            IWebHostEnvironment environment,
            ILogger<MediaMigrationService> logger)
        {
            _db = db;
            _storage = storage;
            _environment = environment;
            _logger = logger;
        }

        public async Task<MediaMigrationStatus> GetStatusAsync(CancellationToken ct = default)
        {
            var status = new MediaMigrationStatus
            {
                TotalImages = await _db.OrderPostImages.CountAsync(ct),
                MigratedCount = await _db.OrderPostImages.CountAsync(x => x.S3Key != null, ct),
            };

            status.PendingCount = status.TotalImages - status.MigratedCount;

            // Only Url is needed to classify a pending row, so pull that column alone rather than
            // materialising entities for what can be a six-figure row count.
            var pendingUrls = await _db.OrderPostImages
                .AsNoTracking()
                .Where(x => x.S3Key == null)
                .OrderBy(x => x.Id)
                .Select(x => x.Url)
                .Take(StatScanCap + 1)
                .ToListAsync(ct);

            status.IsEstimateCapped = pendingUrls.Count > StatScanCap;

            foreach (var url in pendingUrls.Take(StatScanCap))
            {
                var path = ResolvePhysicalPath(url);

                if (path is null)
                {
                    status.NotLocalCount++;
                    continue;
                }

                var info = new FileInfo(path);

                if (info.Exists && info.Length > 0)
                {
                    status.ReadyCount++;
                    status.ReadyBytes += info.Length;
                }
                else
                {
                    status.MissingFileCount++;
                }
            }

            return status;
        }

        /// <summary>
        /// Migrates up to <paramref name="batchSize"/> images with Id greater than
        /// <paramref name="afterId"/>.
        ///
        /// Batched because the whole backlog will not fit in one request, and cursor-paged because
        /// some rows can never migrate — paging on "still pending" would hand back the same
        /// unmigratable rows on every call and never terminate.
        ///
        /// One upload failure is recorded against that image and the batch continues: re-copying
        /// hundreds of files because one of them is unreadable would be worse than a partial pass,
        /// and the next run picks the failure back up.
        /// </summary>
        public async Task<MediaMigrationBatchResult> MigrateBatchAsync(
            int batchSize,
            int afterId,
            string? userId,
            string? userName,
            CancellationToken ct = default)
        {
            batchSize = Math.Clamp(batchSize, 1, 500);

            var result = new MediaMigrationBatchResult { LastProcessedId = afterId };

            var batch = await _db.OrderPostImages
                .Where(x => x.S3Key == null && x.Id > afterId)
                .OrderBy(x => x.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            var now = IstanbulTimeHelper.Now;

            foreach (var image in batch)
            {
                ct.ThrowIfCancellationRequested();

                result.Examined++;
                result.LastProcessedId = image.Id;

                var path = ResolvePhysicalPath(image.Url);

                if (path is null)
                {
                    result.SkippedNotLocal++;
                    continue;
                }

                var info = new FileInfo(path);

                if (!info.Exists || info.Length == 0)
                {
                    result.SkippedMissingFile++;
                    continue;
                }

                try
                {
                    var record = await _storage.UploadLocalFileAsync(
                        path,
                        OrderPostImage.S3Prefix,
                        Path.GetFileName(path),
                        userId,
                        userName,
                        orderId: null,
                        // Detached: this context, not the storage service's, is the one that gets
                        // saved below. ApplicationDbContext is transient, so the two are different
                        // instances and anything the storage service tracked would be dropped.
                        addToIndex: false,
                        ct: ct);

                    // Both writes go through the same context and land in the one SaveChanges at
                    // the end of the batch, so the index can never disagree with the images.
                    _db.S3StoredObjects.Add(record);

                    image.S3Key = record.Key;
                    image.MigratedToS3At = now;

                    result.Migrated++;
                    result.MigratedBytes += info.Length;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;

                    // Cap what goes back to the browser: a systemic failure (bad credentials, say)
                    // would otherwise return one line per file in the batch.
                    if (result.Errors.Count < 20)
                        result.Errors.Add($"#{image.Id} — {ex.Message}");

                    _logger.LogError(
                        ex,
                        "Migrating order-post image {ImageId} ({Path}) to S3 failed.",
                        image.Id,
                        path);
                }
            }

            await _db.SaveChangesAsync(ct);

            result.HasMore = batch.Count == batchSize
                && await _db.OrderPostImages
                    .AnyAsync(x => x.S3Key == null && x.Id > result.LastProcessedId, ct);

            if (result.Migrated > 0 && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Migrated {Count} order-post images to S3 (through id {LastId}) by {User}.",
                    result.Migrated,
                    result.LastProcessedId,
                    userName);
            }

            return result;
        }

        /// <summary>
        /// Rebuilds index rows for objects that images point at but S3StoredObjects does not know
        /// about.
        ///
        /// Needed because the index is bookkeeping, not the source of truth: an image resolves
        /// through OrderPostImages.S3Key and works perfectly with no index row at all. What breaks
        /// is everything computed from the index — file counts, the prefix breakdown, the cost
        /// estimate — and reconciliation, which reports the objects as orphans because from its
        /// side they are indistinguishable from files uploaded by something else entirely.
        ///
        /// The backfill cannot fix this itself: it looks for rows where S3Key IS NULL, and these
        /// rows have a key. They are migrated. Only the receipt is missing.
        ///
        /// Adds nothing that is not really in the bucket — every key is confirmed with a HEAD
        /// first, so a key pointing at a deleted object is reported rather than indexed.
        /// </summary>
        public async Task<IndexRepairResult> RepairIndexAsync(
            string? userId,
            string? userName,
            CancellationToken ct = default)
        {
            var result = new IndexRepairResult();

            var referencedKeys = await _db.OrderPostImages
                .AsNoTracking()
                .Where(x => x.S3Key != null)
                .Select(x => x.S3Key!)
                .Distinct()
                .ToListAsync(ct);

            result.ReferencedKeyCount = referencedKeys.Count;

            if (referencedKeys.Count == 0)
                return result;

            var indexedKeys = await _db.S3StoredObjects
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .Select(x => x.Key)
                .ToListAsync(ct);

            var indexed = indexedKeys.ToHashSet(StringComparer.Ordinal);

            var missing = referencedKeys
                .Where(k => !indexed.Contains(k))
                .ToList();

            result.MissingFromIndexCount = missing.Count;

            var now = IstanbulTimeHelper.Now;

            foreach (var key in missing)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var info = await _storage.TryGetObjectInfoAsync(key, ct);

                    if (info is null)
                    {
                        // The image row points at something that is not in the bucket. Indexing it
                        // would invent a record for a file that does not exist; report instead.
                        result.NotInBucketCount++;

                        if (result.NotInBucketSample.Count < 50)
                            result.NotInBucketSample.Add(key);

                        continue;
                    }

                    var slash = key.IndexOf('/');

                    _db.S3StoredObjects.Add(new S3StoredObject
                    {
                        Key = key,
                        Prefix = slash > 0 ? key[..slash] : OrderPostImage.S3Prefix,
                        OriginalFileName = Path.GetFileName(key),
                        ContentType = info.Value.ContentType,
                        SizeBytes = info.Value.SizeBytes,
                        ETag = info.Value.ETag,
                        // The real upload time is unrecoverable — the row that would have recorded
                        // it is the one that went missing. Stamping the repair time is honest about
                        // that; back-dating from the image row would look like a real measurement.
                        UploadedAt = now,
                        UploadedByUserId = userId,
                        UploadedByUserName = userName,
                    });

                    result.RepairedCount++;
                    result.RepairedBytes += info.Value.SizeBytes;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;

                    if (result.Errors.Count < 20)
                        result.Errors.Add($"{key} — {ex.Message}");

                    _logger.LogError(ex, "Rebuilding index row for {Key} failed.", key);
                }
            }

            if (result.RepairedCount > 0)
            {
                await _db.SaveChangesAsync(ct);

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation(
                        "Rebuilt {Count} S3 index rows ({Bytes} bytes) for {User}.",
                        result.RepairedCount,
                        result.RepairedBytes,
                        userName);
            }

            return result;
        }

        // ------------------------------------------------------------------------------------
        // Registry-driven flows: every media module except the bespoke order-posts backfill.
        //
        // The registry flow differs from the order-posts one in a single deliberate way: it
        // REWRITES the url column to the serving route (preserving its leading-slash convention)
        // instead of leaving it and teaching every read site about the key column. Order posts
        // could afford read-site edits — two controllers. The rest of the system renders these
        // columns from dozens of views, PDFs and the mobile API; rewriting the value they render
        // converts all of them at once. Reversibility is kept by the path-mirrored keys: the
        // original path is derivable from the key, and the local file stays until delete-local.
        // ------------------------------------------------------------------------------------

        private static readonly MethodInfo SpecCountsMethod =
            typeof(MediaMigrationService).GetMethod(nameof(SpecCountsAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly MethodInfo MigrateSpecMethod =
            typeof(MediaMigrationService).GetMethod(nameof(MigrateSpecBatchAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly MethodInfo DeleteLocalSpecMethod =
            typeof(MediaMigrationService).GetMethod(nameof(DeleteLocalSpecBatchAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static string SpecKey(MediaColumnSpec spec) => $"{spec.Entity.Name}.{spec.UrlProperty}";

        public async Task<List<MediaModuleStatus>> GetModuleStatusesAsync(CancellationToken ct = default)
        {
            var results = new List<MediaModuleStatus>();

            foreach (var module in MediaModuleRegistry.Modules)
            {
                var status = new MediaModuleStatus
                {
                    ModuleKey = module.Key,
                    Label = module.Label,
                    Note = module.Note,
                };

                foreach (var spec in module.Columns)
                {
                    ct.ThrowIfCancellationRequested();

                    await (Task)SpecCountsMethod
                        .MakeGenericMethod(spec.Entity)
                        .Invoke(this, new object?[] { spec, status, ct })!;
                }

                results.Add(status);
            }

            return results;
        }

        /// <summary>
        /// One batch of a module's backfill. Uploads each file under its path-mirrored key (once,
        /// however many columns reference it), sets the S3 key column where the spec has one, and
        /// rewrites the url column to the serving route.
        /// </summary>
        public async Task<ModuleBatchResult> MigrateModuleBatchAsync(
            string moduleKey,
            int batchSize,
            Dictionary<string, int>? cursors,
            string? userId,
            string? userName,
            CancellationToken ct = default)
        {
            var module = MediaModuleRegistry.Find(moduleKey)
                ?? throw new ArgumentException($"Unknown media module: {moduleKey}");

            batchSize = Math.Clamp(batchSize, 1, 500);

            var result = new ModuleBatchResult
            {
                ModuleKey = moduleKey,
                Cursors = cursors ?? new Dictionary<string, int>(),
            };

            // Order posts keep their bespoke flow (Url deliberately not rewritten) — route the
            // unified dashboard button through it so the behavior that shipped stays identical.
            if (moduleKey == MediaModuleRegistry.OrderPostsModuleKey)
            {
                var specKey = SpecKey(module.Columns[0]);
                var after = result.Cursors.TryGetValue(specKey, out var c) ? c : 0;

                var legacy = await MigrateBatchAsync(batchSize, after, userId, userName, ct);

                result.Examined = legacy.Examined;
                result.Migrated = legacy.Migrated;
                result.MigratedBytes = legacy.MigratedBytes;
                result.SkippedMissingFile = legacy.SkippedMissingFile;
                result.SkippedNotLocal = legacy.SkippedNotLocal;
                result.FailedCount = legacy.FailedCount;
                result.Errors = legacy.Errors;
                result.Cursors[specKey] = legacy.LastProcessedId;
                result.HasMore = legacy.HasMore;
                return result;
            }

            // Keys confirmed present (index row or uploaded this call) — saves a DB hit per row
            // and stops a file shared between columns from being uploaded twice in one batch.
            var knownKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var spec in module.Columns)
            {
                var remaining = batchSize - result.Examined;
                if (remaining <= 0)
                    break;

                if (spec.IsJsonArray)
                {
                    await MigrateJsonSpecBatchAsync(spec, SpecKey(spec), remaining, result, knownKeys, userId, userName, ct);
                    continue;
                }

                await (Task)MigrateSpecMethod
                    .MakeGenericMethod(spec.Entity)
                    .Invoke(this, new object?[]
                    {
                        spec, SpecKey(spec), moduleKey == "screen-records", remaining, result, knownKeys, userId, userName, ct
                    })!;
            }

            if (result.Migrated > 0 && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Media module {Module}: migrated {Count} files ({Bytes} bytes) to S3 by {User}.",
                    moduleKey, result.Migrated, result.MigratedBytes, userName);
            }

            return result;
        }

        /// <summary>
        /// One batch of deleting local copies of a module's migrated files. Each candidate is
        /// verified against the bucket first — HEAD exists and byte size equals the local file —
        /// and kept (with a reported reason) on any mismatch. <paramref name="confirm"/> = false
        /// is a dry run: same walk, same counts, no deletion.
        /// </summary>
        public async Task<LocalDeleteBatchResult> DeleteLocalModuleBatchAsync(
            string moduleKey,
            int batchSize,
            Dictionary<string, int>? cursors,
            bool confirm,
            CancellationToken ct = default)
        {
            var module = MediaModuleRegistry.Find(moduleKey)
                ?? throw new ArgumentException($"Unknown media module: {moduleKey}");

            batchSize = Math.Clamp(batchSize, 1, 500);

            var result = new LocalDeleteBatchResult
            {
                ModuleKey = moduleKey,
                WasDryRun = !confirm,
                Cursors = cursors ?? new Dictionary<string, int>(),
            };

            foreach (var spec in module.Columns)
            {
                var remaining = batchSize - result.Examined;
                if (remaining <= 0)
                    break;

                await (Task)DeleteLocalSpecMethod
                    .MakeGenericMethod(spec.Entity)
                    .Invoke(this, new object?[] { spec, SpecKey(spec), remaining, result, confirm, ct })!;
            }

            if (result.Deleted > 0)
            {
                _logger.LogWarning(
                    "Media module {Module}: deleted {Count} local files ({Bytes} bytes) after S3 verification.",
                    moduleKey, result.Deleted, result.DeletedBytes);
            }

            return result;
        }

        private async Task SpecCountsAsync<TEntity>(
            MediaColumnSpec spec,
            MediaModuleStatus into,
            CancellationToken ct) where TEntity : class
        {
            var u = spec.UrlProperty;
            var k = spec.KeyProperty;
            var set = _db.Set<TEntity>().AsNoTracking();

            if (spec.IsJsonArray || spec.IsPipeList || spec.IsMediaList)
            {
                // Multi-url columns: "migrated" is detected by content rather than a key column.
                into.Total += await set.CountAsync(
                    e => EF.Property<string>(e, u) != null
                         && EF.Property<string>(e, u) != ""
                         && EF.Property<string>(e, u) != "[]", ct);
                into.Migrated += await set.CountAsync(
                    e => EF.Property<string>(e, u) != null && EF.Property<string>(e, u).Contains("Media/File"), ct);
                into.Pending += await set.CountAsync(
                    e => EF.Property<string>(e, u) != null
                         && EF.Property<string>(e, u) != ""
                         && EF.Property<string>(e, u) != "[]"
                         && !EF.Property<string>(e, u).Contains("Media/File")
                         && EF.Property<string>(e, u).Contains('/'), ct);
                return;
            }

            into.Total += await set.CountAsync(
                e => EF.Property<string>(e, u) != null && EF.Property<string>(e, u) != "", ct);

            if (k is not null)
            {
                into.Migrated += await set.CountAsync(
                    e => EF.Property<string>(e, k) != null
                         || EF.Property<string>(e, u).StartsWith("/Media/File")
                         || EF.Property<string>(e, u).StartsWith("Media/File")
                         || EF.Property<string>(e, u).StartsWith("/OrderPosts/Image"), ct);
            }
            else
            {
                into.Migrated += await set.CountAsync(
                    e => EF.Property<string>(e, u) != null
                         && (EF.Property<string>(e, u).StartsWith("/Media/File")
                             || EF.Property<string>(e, u).StartsWith("Media/File")), ct);
            }

            into.Pending += await set.CountAsync(
                e => EF.Property<string>(e, u) != null
                     && EF.Property<string>(e, u) != ""
                     && (k == null || EF.Property<string>(e, k) == null)
                     && !EF.Property<string>(e, u).StartsWith("/Media/File")
                     && !EF.Property<string>(e, u).StartsWith("Media/File")
                     && !EF.Property<string>(e, u).StartsWith("/OrderPosts/Image")
                     && !EF.Property<string>(e, u).StartsWith("http")
                     && !EF.Property<string>(e, u).StartsWith("//")
                     && !EF.Property<string>(e, u).StartsWith("data:"), ct);
        }

        private async Task MigrateSpecBatchAsync<TEntity>(
            MediaColumnSpec spec,
            string specKey,
            bool requireClosedDay,
            int take,
            ModuleBatchResult result,
            HashSet<string> knownKeys,
            string? userId,
            string? userName,
            CancellationToken ct) where TEntity : class
        {
            var u = spec.UrlProperty;
            var k = spec.KeyProperty;
            var afterId = result.Cursors.TryGetValue(specKey, out var c) ? c : 0;

            var query = _db.Set<TEntity>()
                .Where(e => EF.Property<int>(e, "Id") > afterId
                            && EF.Property<string>(e, u) != null
                            && EF.Property<string>(e, u) != "");

            if (spec.IsPipeList || spec.IsMediaList)
            {
                // One migrate pass converts every local part in the value, so any value already
                // containing the serving route has nothing left to do.
                query = query.Where(e => !EF.Property<string>(e, u).Contains("Media/File"));
            }
            else
            {
                query = query.Where(e => !EF.Property<string>(e, u).StartsWith("/Media/File")
                                         && !EF.Property<string>(e, u).StartsWith("Media/File")
                                         && !EF.Property<string>(e, u).StartsWith("/OrderPosts/Image")
                                         && !EF.Property<string>(e, u).StartsWith("http")
                                         && !EF.Property<string>(e, u).StartsWith("//")
                                         && !EF.Property<string>(e, u).StartsWith("data:"));
            }

            if (k is not null)
                query = query.Where(e => EF.Property<string>(e, k) == null);

            if (requireClosedDay)
            {
                // Screen recording appends chunks to the day's file for as long as the employee
                // works; uploading a file that is still growing stores a truncated recording.
                var today = IstanbulTimeHelper.Now.Date;
                query = query.Where(e => EF.Property<DateTime>(e, "Date") < today);
            }

            var batch = await query
                .OrderBy(e => EF.Property<int>(e, "Id"))
                .Take(take)
                .ToListAsync(ct);

            var urlProp = typeof(TEntity).GetProperty(u)!;
            var keyProp = k is null ? null : typeof(TEntity).GetProperty(k)!;
            var idProp = typeof(TEntity).GetProperty("Id")!;

            foreach (var entity in batch)
            {
                ct.ThrowIfCancellationRequested();

                result.Examined++;
                var id = (int)idProp.GetValue(entity)!;
                result.Cursors[specKey] = id;

                var url = (string?)urlProp.GetValue(entity);

                if (spec.IsPipeList || spec.IsMediaList)
                {
                    // Multi-url columns: employee errors use "a||b||c", product images mix a plain
                    // url, a "||" list, and a JSON array. Each part is migrated like a column of
                    // its own; external parts stay in place, and the value is serialized back in
                    // its column's own convention.
                    var parts = ParseMultiUrlValue(url, spec.IsMediaList).ToArray();

                    var changed = false;
                    var missing = false;

                    try
                    {
                        for (var i = 0; i < parts.Length; i++)
                        {
                            var partPath = ResolvePhysicalPath(parts[i]);

                            if (partPath is null)
                                continue;

                            var partInfo = new FileInfo(partPath);

                            if (!partInfo.Exists || partInfo.Length == 0)
                            {
                                missing = true;
                                continue;
                            }

                            var partKey = await EnsureUploadedAsync(spec, partPath, knownKeys, userId, userName, ct);

                            parts[i] = MediaModuleRegistry.BuildServingUrlLike(partKey, parts[i]);
                            changed = true;
                            result.MigratedBytes += partInfo.Length;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;

                        if (result.Errors.Count < 20)
                            result.Errors.Add($"{specKey}#{id} — {ex.Message}");

                        _logger.LogError(ex, "Migrating {Spec} row {Id} to S3 failed.", specKey, id);
                        continue;
                    }

                    if (changed)
                    {
                        urlProp.SetValue(entity, SerializeMultiUrlValue(parts, spec.IsMediaList));
                        result.Migrated++;
                    }
                    else if (missing)
                    {
                        result.SkippedMissingFile++;
                    }
                    else
                    {
                        result.SkippedNotLocal++;
                    }

                    continue;
                }

                var path = ResolvePhysicalPath(url);

                if (path is null)
                {
                    result.SkippedNotLocal++;
                    continue;
                }

                var info = new FileInfo(path);

                if (!info.Exists || info.Length == 0)
                {
                    result.SkippedMissingFile++;
                    continue;
                }

                try
                {
                    var key = await EnsureUploadedAsync(spec, path, knownKeys, userId, userName, ct);

                    keyProp?.SetValue(entity, key);
                    urlProp.SetValue(entity, MediaModuleRegistry.BuildServingUrlLike(key, url));

                    result.Migrated++;
                    result.MigratedBytes += info.Length;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;

                    if (result.Errors.Count < 20)
                        result.Errors.Add($"{specKey}#{id} — {ex.Message}");

                    _logger.LogError(ex, "Migrating {Spec} row {Id} ({Path}) to S3 failed.", specKey, id, path);
                }
            }

            await _db.SaveChangesAsync(ct);

            if (batch.Count == take)
                result.HasMore = true;
        }

        /// <summary>
        /// The one JSON-array column (EmployeeTask.AttachmentImagesJson): each element is treated
        /// like a url column of its own. A row counts as migrated once every local element has
        /// been replaced; elements that are external URLs are left in place.
        /// </summary>
        private async Task MigrateJsonSpecBatchAsync(
            MediaColumnSpec spec,
            string specKey,
            int take,
            ModuleBatchResult result,
            HashSet<string> knownKeys,
            string? userId,
            string? userName,
            CancellationToken ct)
        {
            var afterId = result.Cursors.TryGetValue(specKey, out var c) ? c : 0;

            var batch = await _db.Set<EmployeeTask>()
                .Where(t => t.Id > afterId
                            && t.AttachmentImagesJson != null
                            && t.AttachmentImagesJson != "[]"
                            && !t.AttachmentImagesJson.Contains("Media/File"))
                .OrderBy(t => t.Id)
                .Take(take)
                .ToListAsync(ct);

            foreach (var task in batch)
            {
                ct.ThrowIfCancellationRequested();

                result.Examined++;
                result.Cursors[specKey] = task.Id;

                List<string>? urls;

                try
                {
                    urls = JsonSerializer.Deserialize<List<string>>(task.AttachmentImagesJson!);
                }
                catch (JsonException)
                {
                    result.SkippedNotLocal++;
                    continue;
                }

                if (urls is null || urls.Count == 0)
                {
                    result.SkippedNotLocal++;
                    continue;
                }

                var changed = false;
                var missing = false;

                try
                {
                    for (var i = 0; i < urls.Count; i++)
                    {
                        var path = ResolvePhysicalPath(urls[i]);

                        if (path is null)
                            continue;

                        var info = new FileInfo(path);

                        if (!info.Exists || info.Length == 0)
                        {
                            missing = true;
                            continue;
                        }

                        var key = await EnsureUploadedAsync(spec, path, knownKeys, userId, userName, ct);

                        urls[i] = MediaModuleRegistry.BuildServingUrlLike(key, urls[i]);
                        changed = true;
                        result.MigratedBytes += info.Length;
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;

                    if (result.Errors.Count < 20)
                        result.Errors.Add($"{specKey}#{task.Id} — {ex.Message}");

                    _logger.LogError(ex, "Migrating {Spec} row {Id} to S3 failed.", specKey, task.Id);
                    continue;
                }

                if (changed)
                {
                    task.AttachmentImagesJson = JsonSerializer.Serialize(urls);
                    result.Migrated++;
                }
                else if (missing)
                {
                    result.SkippedMissingFile++;
                }
                else
                {
                    result.SkippedNotLocal++;
                }
            }

            await _db.SaveChangesAsync(ct);

            if (batch.Count == take)
                result.HasMore = true;
        }

        private async Task DeleteLocalSpecBatchAsync<TEntity>(
            MediaColumnSpec spec,
            string specKey,
            int take,
            LocalDeleteBatchResult result,
            bool confirm,
            CancellationToken ct) where TEntity : class
        {
            var u = spec.UrlProperty;
            var k = spec.KeyProperty;
            var afterId = result.Cursors.TryGetValue(specKey, out var c) ? c : 0;

            var query = _db.Set<TEntity>().AsNoTracking()
                .Where(e => EF.Property<int>(e, "Id") > afterId);

            if (spec.IsJsonArray || spec.IsPipeList)
            {
                query = query.Where(e => EF.Property<string>(e, u) != null
                                         && EF.Property<string>(e, u).Contains("Media/File"));
            }
            else if (k is not null)
            {
                query = query.Where(e => EF.Property<string>(e, k) != null
                                         || (EF.Property<string>(e, u) != null
                                             && (EF.Property<string>(e, u).StartsWith("/Media/File")
                                                 || EF.Property<string>(e, u).StartsWith("Media/File"))));
            }
            else
            {
                query = query.Where(e => EF.Property<string>(e, u) != null
                                         && (EF.Property<string>(e, u).StartsWith("/Media/File")
                                             || EF.Property<string>(e, u).StartsWith("Media/File")));
            }

            var rows = await query
                .OrderBy(e => EF.Property<int>(e, "Id"))
                .Take(take)
                .Select(e => new
                {
                    Id = EF.Property<int>(e, "Id"),
                    Url = EF.Property<string>(e, u),
                    Key = k == null ? null : EF.Property<string>(e, k),
                })
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();

                result.Examined++;
                result.Cursors[specKey] = row.Id;

                // A row can carry several keys (the JSON column) but usually carries one.
                var keys = new List<string>();

                if (spec.IsJsonArray)
                {
                    try
                    {
                        foreach (var url in JsonSerializer.Deserialize<List<string>>(row.Url!) ?? new())
                        {
                            var extracted = MediaModuleRegistry.TryExtractKey(url);
                            if (extracted is not null)
                                keys.Add(extracted);
                        }
                    }
                    catch (JsonException)
                    {
                        continue;
                    }
                }
                else if (spec.IsPipeList || spec.IsMediaList)
                {
                    foreach (var part in ParseMultiUrlValue(row.Url, spec.IsMediaList))
                    {
                        var extracted = MediaModuleRegistry.TryExtractKey(part);
                        if (extracted is not null)
                            keys.Add(extracted);
                    }
                }
                else
                {
                    var single = row.Key ?? MediaModuleRegistry.TryExtractKey(row.Url);
                    if (single is not null)
                        keys.Add(single);
                }

                foreach (var key in keys)
                {
                    try
                    {
                        // Prefer the row's own url when it still names a local file (order posts
                        // keep the original path there); otherwise the mirrored key names it.
                        var path = ResolvePhysicalPath(spec.IsJsonArray ? null : row.Url);

                        if (path is null)
                        {
                            var rel = MediaModuleRegistry.DeriveRelativePath(spec, key);
                            path = rel is null ? null : PhysicalPathFromRelative(rel);
                        }

                        if (path is null || !System.IO.File.Exists(path))
                        {
                            result.AlreadyGone++;
                            continue;
                        }

                        var localSize = new FileInfo(path).Length;
                        var head = await _storage.TryGetObjectInfoAsync(key, ct);

                        if (head is null)
                        {
                            result.KeptNotInBucket++;

                            if (result.KeptNotInBucketSample.Count < 20)
                                result.KeptNotInBucketSample.Add(key);

                            continue;
                        }

                        if (head.Value.SizeBytes != localSize)
                        {
                            result.KeptSizeMismatch++;

                            if (result.KeptSizeMismatchSample.Count < 20)
                                result.KeptSizeMismatchSample.Add(key);

                            continue;
                        }

                        result.Deletable++;
                        result.DeletableBytes += localSize;

                        if (confirm)
                        {
                            System.IO.File.Delete(path);
                            result.Deleted++;
                            result.DeletedBytes += localSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;

                        if (result.Errors.Count < 20)
                            result.Errors.Add($"{specKey}#{row.Id} — {ex.Message}");

                        _logger.LogError(ex, "Deleting local copy for {Spec} row {Id} failed.", specKey, row.Id);
                    }
                }
            }

            if (rows.Count == take)
                result.HasMore = true;
        }

        /// <summary>
        /// Uploads the file under its path-mirrored key unless the index already knows the key —
        /// which happens whenever a second row references the same file. The index row goes
        /// through this context so it lands in the same SaveChanges as the column updates.
        /// </summary>
        private async Task<string> EnsureUploadedAsync(
            MediaColumnSpec spec,
            string physicalPath,
            HashSet<string> knownKeys,
            string? userId,
            string? userName,
            CancellationToken ct)
        {
            var rel = Path.GetRelativePath(WebRootPath(), physicalPath);
            var key = MediaModuleRegistry.DeriveKey(spec, rel);

            if (knownKeys.Contains(key))
                return key;

            if (await _db.S3StoredObjects.AnyAsync(x => x.Key == key && !x.IsDeleted, ct))
            {
                knownKeys.Add(key);
                return key;
            }

            var record = await _storage.UploadLocalFileAsync(
                physicalPath,
                spec.Prefix,
                Path.GetFileName(physicalPath),
                userId,
                userName,
                orderId: null,
                // Same transient-context contract as the order-posts backfill: the record must be
                // saved by THIS context, alongside the column updates.
                addToIndex: false,
                explicitKey: key,
                ct);

            _db.S3StoredObjects.Add(record);
            knownKeys.Add(key);

            return key;
        }

        /// <summary>
        /// Splits a multi-url column value. Pipe columns are "a||b||c"; media-list columns
        /// (product images) additionally allow a JSON array, matching
        /// ProductImagesController.ParseMediaValueList.
        /// </summary>
        private static List<string> ParseMultiUrlValue(string? value, bool allowJson)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(value))
                return result;

            value = value.Trim();

            if (allowJson && value.StartsWith('['))
            {
                try
                {
                    return (JsonSerializer.Deserialize<List<string>>(value) ?? new())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .ToList();
                }
                catch (JsonException)
                {
                    // Not valid JSON after all — fall through to the delimiter forms.
                }
            }

            return value
                .Split(new[] { "||", "|" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }

        /// <summary>Inverse of <see cref="ParseMultiUrlValue"/>, in the column's own convention:
        /// "||" for pipe columns; plain-if-one / JSON-if-many for media-list columns.</summary>
        private static string SerializeMultiUrlValue(string[] parts, bool isMediaList)
        {
            if (!isMediaList)
                return string.Join("||", parts);

            return parts.Length == 1 ? parts[0] : JsonSerializer.Serialize(parts);
        }

        private string WebRootPath() =>
            string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _environment.WebRootPath;

        /// <summary>Containment-checked absolute path for a wwwroot-relative one, or null.</summary>
        private string? PhysicalPathFromRelative(string relative)
        {
            try
            {
                var root = Path.GetFullPath(WebRootPath());
                var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

                var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
                    ? root
                    : root + Path.DirectorySeparatorChar;

                return full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ? full : null;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        /// <summary>
        /// Maps a stored Url to a file under wwwroot, or null when it does not name one.
        ///
        /// Returns null — rather than throwing — for absolute URLs and for the S3 serving route,
        /// both of which turn up legitimately: sharing a post to another employee copies the
        /// source row's Url verbatim, so an already-migrated image can reappear as a serving route
        /// on a brand-new row.
        ///
        /// The containment check is not paranoia about our own data. Url values reach the database
        /// through the dynamic-SQL quick-report paths as well as EF, so this method treats them as
        /// untrusted input and refuses anything that escapes wwwroot.
        /// </summary>
        private string? ResolvePhysicalPath(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            url = url.Trim();

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("//", StringComparison.Ordinal)
                || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Already pointing at S3 through a redirect route (either of them).
            if (MediaModuleRegistry.IsServingUrl(url))
                return null;

            // A query string or fragment is never part of a path on disk.
            var cut = url.AsSpan().IndexOfAny('?', '#');
            if (cut >= 0)
                url = url[..cut];

            var relative = Uri.UnescapeDataString(url).TrimStart('/', '\\');

            if (string.IsNullOrWhiteSpace(relative))
                return null;

            var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _environment.WebRootPath;

            string fullPath;
            string rootPath;

            try
            {
                rootPath = Path.GetFullPath(webRoot);
                fullPath = Path.GetFullPath(Path.Combine(rootPath, relative));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }

            // Compare against the root plus a separator so "wwwroot-backup" cannot pass as being
            // inside "wwwroot".
            var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }
    }
