using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Time;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Luxira.Api.Features.Media.Services;
    /// <summary>
    /// Clears media references whose file no longer exists anywhere.
    ///
    /// The problem it solves: a row can name a wwwroot file that has since been deleted. Nothing
    /// notices — the column is non-null, so the `?? "static/DefaultImage.svg"` fallback every read
    /// path relies on never fires, and the client is handed a URL guaranteed to 404. Emptying the
    /// column hands those read paths the placeholder they already know how to serve, which is why
    /// this fixes every consumer at once without any of them changing.
    ///
    /// It is deliberately timid, because it runs unattended on a timer and deletes business data:
    ///
    ///   * Only single-URL columns. Multi-URL columns (JSON / "||" lists) would need partial
    ///     rewriting, and a bug there corrupts a list instead of clearing one field.
    ///   * Only rows with no S3 key and a legacy local path. Anything already served from S3, or
    ///     pointing at an external host, is out of scope.
    ///   * Missing on disk is not enough — the mirrored key must also be absent from the bucket. A
    ///     file gone locally but present in S3 is un-migrated, not dead.
    ///   * If the bucket cannot be reached, the run clears nothing. It cannot prove absence, so it
    ///     does not act.
    ///   * If the findings look like an environment fault rather than data rot (see
    ///     <see cref="MaxClearsPerRun"/>), the run aborts having written nothing.
    ///   * By default it does not write at all. Dry run is the shipped mode: the sweep reports the
    ///     references it would have cleared and stops, and stays that way until an admin turns it
    ///     off from the dashboard.
    ///
    /// Every cleared value is stored on the run row, so a bad run is undone from that record
    /// rather than from a database restore.
    /// </summary>
    public class MediaReferenceCleanupService
    {
        /// <summary>
        /// Rows read per run, across all columns. Bounds both the table scans and the stat() calls;
        /// the cursor carries the remainder into the next run.
        ///
        /// Sized for one pass a day. Orders alone is tens of thousands of rows, and a cap low
        /// enough to be invisible per-run would take weeks to walk the corpus once — the sweep has
        /// to actually reach the tail to be worth running.
        /// </summary>
        private const int MaxRowsPerRun = 40_000;

        /// <summary>
        /// Most references a single run will clear. Genuine rot is a trickle — a flood means the
        /// sweep is wrong about its own inputs (wwwroot not mounted, WebRootPath misconfigured),
        /// and acting on that would erase thousands of good references. Past this it writes nothing
        /// and records why, leaving the decision to an admin.
        /// </summary>
        private const int MaxClearsPerRun = 700;

        /// <summary>SQL Server's "cannot insert NULL into a NOT NULL column" error.</summary>
        private const int NullConstraintError = 515;

        /// <summary>Ids per UPDATE, kept well inside SQL Server's parameter ceiling.</summary>
        private const int ClearBatchSize = 200;

        /// <summary>
        /// Below this many local-path rows the all-missing ratio check is not meaningful — a
        /// handful of dead rows is ordinary and proves nothing about the environment.
        /// </summary>
        private const int RatioGuardMinimumSample = 20;

        /// <summary>The settings table holds one row; this is it.</summary>
        private const int SettingRowId = 1;

        private readonly ApplicationDbContext _db;
        private readonly S3StorageService _storage;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<MediaReferenceCleanupService> _logger;

        public MediaReferenceCleanupService(
            ApplicationDbContext db,
            S3StorageService storage,
            IWebHostEnvironment environment,
            ILogger<MediaReferenceCleanupService> logger)
        {
            _db = db;
            _storage = storage;
            _environment = environment;
            _logger = logger;
        }

        private sealed class Candidate
        {
            public MediaColumnSpec Spec { get; init; } = null!;
            public string SpecKey { get; init; } = null!;
            public int Id { get; init; }
            public string Url { get; init; } = null!;
        }

        /// <summary>Mutable counters threaded through the generic per-column scan.</summary>
        private sealed class ScanTotals
        {
            public int LocalPathRowsSeen { get; set; }
        }

        private static readonly MethodInfo ScanSpecMethod =
            typeof(MediaReferenceCleanupService)
                .GetMethod(nameof(ScanSpecAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static readonly MethodInfo ClearSpecMethod =
            typeof(MediaReferenceCleanupService)
                .GetMethod(nameof(ClearSpecAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        private static string SpecKey(MediaColumnSpec spec) => $"{spec.Entity.Name}.{spec.UrlProperty}";

        /// <summary>
        /// One sweep. Always returns a persisted run row — including for aborts and failures, since
        /// a run that declined to act is exactly what an admin needs to see.
        /// </summary>
        public async Task<MediaReferenceCleanupRun> RunAsync(
            string triggeredBy,
            CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();

            var run = new MediaReferenceCleanupRun
            {
                StartedAt = IstanbulTimeHelper.Now,
                TriggeredBy = string.IsNullOrWhiteSpace(triggeredBy) ? "auto" : triggeredBy,
                IsDryRun = await GetDryRunAsync(ct),
            };

            try
            {
                var cursors = await LoadCursorsAsync(ct);
                var candidates = new List<Candidate>();
                var totals = new ScanTotals();

                foreach (var spec in MediaModuleRegistry.Modules.SelectMany(m => m.Columns))
                {
                    ct.ThrowIfCancellationRequested();

                    // Multi-URL columns are out of scope; see the class remarks.
                    if (spec.IsJsonArray || spec.IsPipeList || spec.IsMediaList)
                        continue;

                    var remaining = MaxRowsPerRun - run.RowsScanned;

                    if (remaining <= 0)
                    {
                        run.ScanWasCapped = true;
                        break;
                    }

                    await (Task)ScanSpecMethod
                        .MakeGenericMethod(spec.Entity)
                        .Invoke(this, new object?[]
                        {
                            spec, SpecKey(spec), remaining, run, cursors, candidates, totals, ct
                        })!;
                }

                run.CursorsJson = JsonSerializer.Serialize(cursors);

                if (candidates.Count == 0)
                {
                    await FinishAsync(run, stopwatch, ct);
                    return run;
                }

                // A flood of missing files is an environment fault, and the bucket cannot tell us
                // otherwise. Stop here rather than spending one HEAD request per candidate to
                // confirm a conclusion we have already decided not to act on.
                if (candidates.Count > MaxClearsPerRun)
                {
                    run.WasAborted = true;
                    run.AbortReason =
                        $"عدد الملفات المفقودة على القرص ({candidates.Count}) تجاوز الحد الآمن "
                        + $"({MaxClearsPerRun}) — لم يتم التحقق من S3 ولم يتم مسح أي مرجع؛ "
                        + "يُرجّح وجود خلل في الوصول للملفات وليس تلفًا في البيانات.";

                    _logger.LogWarning(
                        "Media cleanup aborted before S3 verification: {Count} missing files exceeds cap {Cap}.",
                        candidates.Count, MaxClearsPerRun);

                    await FinishAsync(run, stopwatch, ct);
                    return run;
                }

                // Absent from disk is only half the test. Ask the bucket before touching anything.
                var confirmed = new List<Candidate>();

                foreach (var candidate in candidates)
                {
                    ct.ThrowIfCancellationRequested();

                    var relative = ToWwwrootRelative(candidate.Url);

                    if (relative is null)
                        continue;

                    var key = MediaModuleRegistry.DeriveKey(candidate.Spec, relative);

                    bool inBucket;

                    try
                    {
                        inBucket = await _storage.ExistsAsync(key, ct);
                    }
                    catch (Exception ex)
                    {
                        // Absence could not be established, so nothing is provably dead. Fail
                        // closed rather than deleting references on the strength of an outage.
                        _logger.LogError(ex, "Media cleanup could not reach S3; clearing nothing this run.");

                        run.WasAborted = true;
                        run.AbortReason = "تعذر الوصول إلى S3 للتحقق، لم يتم مسح أي مرجع.";
                        run.Error = Truncate(ex.Message, 2000);

                        await FinishAsync(run, stopwatch, ct);
                        return run;
                    }

                    if (inBucket)
                        run.SkippedStillInBucket++;
                    else
                        confirmed.Add(candidate);
                }

                if (confirmed.Count == 0)
                {
                    await FinishAsync(run, stopwatch, ct);
                    return run;
                }

                var abort = EvaluateSafety(confirmed.Count, totals.LocalPathRowsSeen);

                if (abort is not null)
                {
                    _logger.LogWarning(
                        "Media cleanup aborted without writing: {Reason} ({Count} candidates of {Seen} local-path rows).",
                        abort, confirmed.Count, totals.LocalPathRowsSeen);

                    run.WasAborted = true;
                    run.AbortReason = abort;

                    await FinishAsync(run, stopwatch, ct);
                    return run;
                }

                run.WouldClearCount = confirmed.Count;

                // Record what is about to be nulled before nulling it, so the undo record exists
                // even if the write half fails partway. In a dry run this list is the deliverable.
                run.ClearedEntriesJson = JsonSerializer.Serialize(confirmed.Select(c => new
                {
                    entity = c.Spec.Entity.Name,
                    column = c.Spec.UrlProperty,
                    id = c.Id,
                    url = c.Url,
                }));

                if (run.IsDryRun)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                        _logger.LogInformation(
                            "Media cleanup dry run: {Count} references would have been cleared.",
                            confirmed.Count);

                    await FinishAsync(run, stopwatch, ct);
                    return run;
                }

                foreach (var group in confirmed.GroupBy(c => c.SpecKey))
                {
                    ct.ThrowIfCancellationRequested();

                    var spec = group.First().Spec;
                    var ids = group.Select(c => c.Id).ToList();

                    try
                    {
                        await (Task)ClearSpecMethod
                            .MakeGenericMethod(spec.Entity)
                            .Invoke(this, new object?[] { spec, ids, run, ct })!;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Media cleanup failed to clear {Spec}.", group.Key);
                        run.FailedCount += ids.Count;
                    }
                }

                if (run.ReferencesCleared > 0)
                {
                    _logger.LogWarning(
                        "Media cleanup cleared {Count} dead references (triggered by {By}).",
                        run.ReferencesCleared, run.TriggeredBy);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media reference cleanup run failed.");
                run.Error = Truncate(ex.Message, 2000);
            }

            await FinishAsync(run, stopwatch, ct);
            return run;
        }

        /// <summary>
        /// Why this run must not write, or null if it may proceed. Both checks describe the same
        /// suspicion from different angles: that the filesystem, not the data, is what changed.
        /// </summary>
        private static string? EvaluateSafety(int clearCount, int localPathRowsSeen)
        {
            if (clearCount > MaxClearsPerRun)
            {
                return $"عدد المراجع المرشحة ({clearCount}) تجاوز الحد الآمن ({MaxClearsPerRun}) — "
                       + "يُرجّح وجود خلل في الوصول للملفات وليس تلفًا في البيانات.";
            }

            // Rot affects scattered rows. Every local file being absent at once is what a missing
            // mount looks like, so treat it as one.
            if (localPathRowsSeen >= RatioGuardMinimumSample && clearCount == localPathRowsSeen)
            {
                return $"كل الملفات المحلية التي تم فحصها ({localPathRowsSeen}) بدت مفقودة — "
                       + "يُرجّح أن مجلد الملفات غير متاح.";
            }

            return null;
        }

        private async Task ScanSpecAsync<TEntity>(
            MediaColumnSpec spec,
            string specKey,
            int take,
            MediaReferenceCleanupRun run,
            Dictionary<string, int> cursors,
            List<Candidate> candidates,
            ScanTotals totals,
            CancellationToken ct) where TEntity : class
        {
            var u = spec.UrlProperty;
            var k = spec.KeyProperty;
            var afterId = cursors.TryGetValue(specKey, out var c) ? c : 0;

            var query = _db.Set<TEntity>().AsNoTracking()
                .Where(e => EF.Property<int>(e, "Id") > afterId
                            && EF.Property<string>(e, u) != null
                            && EF.Property<string>(e, u) != ""
                            && !EF.Property<string>(e, u).StartsWith("/Media/File")
                            && !EF.Property<string>(e, u).StartsWith("Media/File")
                            && !EF.Property<string>(e, u).StartsWith("/OrderPosts/Image")
                            && !EF.Property<string>(e, u).StartsWith("http")
                            && !EF.Property<string>(e, u).StartsWith("//")
                            && !EF.Property<string>(e, u).StartsWith("data:"));

            // A row carrying an S3 key is migrated; its file living in the bucket is the norm, not
            // a fault. Only never-migrated rows are in scope.
            if (k is not null)
                query = query.Where(e => EF.Property<string>(e, k) == null);

            var rows = await query
                .OrderBy(e => EF.Property<int>(e, "Id"))
                .Take(take)
                .Select(e => new
                {
                    Id = EF.Property<int>(e, "Id"),
                    Url = EF.Property<string>(e, u),
                })
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                run.RowsScanned++;
                cursors[specKey] = row.Id;

                var path = ResolvePhysicalPath(row.Url);

                // Outside wwwroot or otherwise not a local reference — not this sweep's business.
                if (path is null)
                    continue;

                totals.LocalPathRowsSeen++;

                if (!File.Exists(path))
                {
                    candidates.Add(new Candidate
                    {
                        Spec = spec,
                        SpecKey = specKey,
                        Id = row.Id,
                        Url = row.Url,
                    });
                }
            }

            // Fewer rows than asked for means the end of this column; start the next run at the top
            // so rows behind the cursor that break later are eventually revisited.
            if (rows.Count < take)
                cursors[specKey] = 0;
        }

        /// <summary>
        /// Clears one column's worth of dead references.
        ///
        /// Null is the honest value for "there is no file here", and it is the one the read paths'
        /// fallbacks are waiting for, so it is what we write wherever the column admits it. A few
        /// of these columns are NOT NULL and cannot say it; there the empty string carries the same
        /// meaning to every consumer, and the scanner skips empty strings, so a row cleared that way
        /// is not picked up again on the next pass.
        ///
        /// Which columns those are is not taken on the model's word. This schema drifts — at least
        /// one column is NOT NULL in production without the model knowing it — so where the model
        /// claims a column is nullable we try null and let the database correct us. On its own
        /// constraints the database is the authority.
        /// </summary>
        private async Task ClearSpecAsync<TEntity>(
            MediaColumnSpec spec,
            List<int> ids,
            MediaReferenceCleanupRun run,
            CancellationToken ct) where TEntity : class
        {
            var required = _db.Model
                .FindEntityType(typeof(TEntity))
                ?.FindProperty(spec.UrlProperty)
                ?.IsNullable == false;

            try
            {
                await WriteBlankAsync<TEntity>(spec, ids, run, required ? string.Empty : null, ct);
            }
            catch (SqlException ex) when (!required && ex.Number == NullConstraintError)
            {
                _logger.LogWarning(
                    "Media cleanup: {Spec} rejects NULL in the database; clearing to empty string.",
                    SpecKey(spec));

                await WriteBlankAsync<TEntity>(spec, ids, run, string.Empty, ct);
            }
        }

        /// <summary>
        /// Writes the blank value straight to the column with a targeted UPDATE.
        ///
        /// Deliberately not through the change tracker. Loading the entity means materialising
        /// every other column on the row, and this schema has columns the model calls required
        /// that hold NULL in practice — CallRecordings.OtherPartyPhone is one — so the load throws
        /// before the write is even attempted, over a column the sweep has no interest in. Writing
        /// one column by name touches only what we mean to touch.
        ///
        /// It also makes a failure local by construction: nothing is tracked, so a rejected
        /// statement cannot ride along inside the next column's batch the way a failed
        /// SaveChanges could.
        /// </summary>
        private async Task<int> WriteBlankAsync<TEntity>(
            MediaColumnSpec spec,
            List<int> ids,
            MediaReferenceCleanupRun run,
            string? blank,
            CancellationToken ct) where TEntity : class
        {
            var entityType = _db.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException($"No entity type for {typeof(TEntity).Name}.");

            var table = entityType.GetTableName()
                ?? throw new InvalidOperationException($"No table for {typeof(TEntity).Name}.");

            var schema = entityType.GetSchema() ?? "dbo";
            var store = StoreObjectIdentifier.Table(table, entityType.GetSchema());

            var column = entityType.FindProperty(spec.UrlProperty)?.GetColumnName(store)
                ?? spec.UrlProperty;

            var affected = 0;

            foreach (var chunk in ids.Chunk(ClearBatchSize))
            {
                var placeholders = new string[chunk.Length];
                var parameters = new List<object>(chunk.Length + 1)
                {
                    new SqlParameter("@blank", (object?)blank ?? DBNull.Value),
                };

                for (var i = 0; i < chunk.Length; i++)
                {
                    placeholders[i] = "@id" + i;
                    parameters.Add(new SqlParameter(placeholders[i], chunk[i]));
                }

                // Identifiers come from the EF model rather than from anything user-supplied; the
                // values are parameterised.
                var sql =
                    $"UPDATE [{Escape(schema)}].[{Escape(table)}] SET [{Escape(column)}] = @blank "
                    + $"WHERE [Id] IN ({string.Join(", ", placeholders)})";

                affected += await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
            }

            run.ReferencesCleared += affected;
            return affected;
        }

        private static string Escape(string identifier) => identifier.Replace("]", "]]");

        /// <summary>
        /// Current mode, plus who last set it. Returns the fail-safe (dry run) when the row has
        /// never been written.
        /// </summary>
        public async Task<MediaReferenceCleanupSetting> GetSettingAsync(CancellationToken ct = default)
        {
            var setting = await _db.MediaReferenceCleanupSettings
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct);

            return setting ?? new MediaReferenceCleanupSetting { Id = SettingRowId, DryRun = true };
        }

        /// <summary>
        /// Switches the sweep between reporting and writing, recording who did it.
        ///
        /// Turning dry run off is what makes an unattended job start deleting, so the change is
        /// attributed and logged at warning level rather than applied silently.
        /// </summary>
        public async Task<MediaReferenceCleanupSetting> SetDryRunAsync(
            bool dryRun,
            string? changedBy,
            CancellationToken ct = default)
        {
            var setting = await _db.MediaReferenceCleanupSettings
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct);

            if (setting is null)
            {
                setting = new MediaReferenceCleanupSetting();
                _db.MediaReferenceCleanupSettings.Add(setting);
            }

            setting.DryRun = dryRun;
            setting.UpdatedAt = IstanbulTimeHelper.Now;
            setting.UpdatedBy = Truncate(string.IsNullOrWhiteSpace(changedBy) ? "admin" : changedBy, 256);

            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Media reference cleanup dry-run mode set to {DryRun} by {By}.",
                dryRun, setting.UpdatedBy);

            return setting;
        }

        /// <summary>
        /// Reads the mode, treating any failure as dry run. If we cannot establish that deleting
        /// was authorised, we have not established it — an unreadable setting is not consent.
        /// </summary>
        private async Task<bool> GetDryRunAsync(CancellationToken ct)
        {
            try
            {
                return (await GetSettingAsync(ct)).DryRun;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read media cleanup mode; falling back to dry run.");
                return true;
            }
        }

        private async Task<Dictionary<string, int>> LoadCursorsAsync(CancellationToken ct)
        {
            var json = await _db.MediaReferenceCleanupRuns
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => x.CursorsJson)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, int>(StringComparer.Ordinal);

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                       ?? new Dictionary<string, int>(StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                // A malformed cursor costs one re-scan from the top, which is safe.
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }
        }

        private async Task FinishAsync(
            MediaReferenceCleanupRun run,
            Stopwatch stopwatch,
            CancellationToken ct)
        {
            stopwatch.Stop();

            run.DurationMs = stopwatch.ElapsedMilliseconds;
            run.CompletedAt = IstanbulTimeHelper.Now;

            _db.MediaReferenceCleanupRuns.Add(run);
            await _db.SaveChangesAsync(ct);
        }

        private static string? Truncate(string? value, int max) =>
            value is null || value.Length <= max ? value : value[..max];

        /// <summary>wwwroot-relative form of a stored URL, or null if it is not a local reference.</summary>
        private string? ToWwwrootRelative(string? url)
        {
            var full = ResolvePhysicalPath(url);

            if (full is null)
                return null;

            var root = Path.GetFullPath(WebRoot());

            var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            return full[prefix.Length..].Replace('\\', '/');
        }

        private string WebRoot() =>
            string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
                : _environment.WebRootPath;

        /// <summary>
        /// Absolute path for a stored URL, or null when it does not name a file inside wwwroot.
        /// Mirrors MediaMigrationService.ResolvePhysicalPath — same exclusions, same containment
        /// check, so both agree on what "a local file" means.
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

            if (MediaModuleRegistry.IsServingUrl(url))
                return null;

            var cut = url.AsSpan().IndexOfAny('?', '#');
            if (cut >= 0)
                url = url[..cut];

            var relative = Uri.UnescapeDataString(url).TrimStart('/', '\\');

            if (string.IsNullOrWhiteSpace(relative))
                return null;

            string fullPath;
            string rootPath;

            try
            {
                rootPath = Path.GetFullPath(WebRoot());
                fullPath = Path.GetFullPath(Path.Combine(rootPath, relative));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }

            var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }
    }
