using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Luxira.Api.Features.Media.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.BackgroundServices;
    /// <summary>
    /// Uploads finished screen recordings to S3, then frees the local disk copy once that upload
    /// is verified.
    ///
    /// Every other media module reaches the bucket through FileUploadService, which uploads the
    /// whole file in the request that produced it. Screen recording cannot: UploadChunk appends to
    /// one file per employee per day for the length of a shift, so there is no point during the
    /// day at which the file is complete. That is why the S3 switch converted this module's read,
    /// delete and retention paths but left its write path on disk — and why, with no trigger of
    /// its own, nothing reached the bucket unless someone pressed the dashboard button.
    ///
    /// This service is that trigger. "Finalize" is the close of the recording day: once the date
    /// has passed, UploadChunk can no longer route a chunk to that file, so it is safe to upload.
    /// Uploading sooner would store a truncated recording.
    ///
    /// The migration itself is not reimplemented here — MigrateModuleBatchAsync with the registry's
    /// requireClosedDay rule already does exactly this work, and reusing it keeps the automatic
    /// path and the dashboard button on identical behaviour.
    ///
    /// After a pass uploads anything, it immediately runs DeleteLocalModuleBatchAsync — the same
    /// HEAD-plus-byte-size check the dashboard's delete-local button uses. A file only leaves disk
    /// once its S3 copy is confirmed intact; a failed or truncated upload is left in place rather
    /// than deleted.
    /// </summary>
    public class ScreenRecordS3UploadService : BackgroundService
    {
        private const string ModuleKey = "screen-records";

        /// <summary>Rows per batch. Each one is a multi-hundred-MB upload, so this stays small.</summary>
        private const int BatchSize = 25;

        /// <summary>
        /// Stops a single pass from running forever if a row keeps reporting more work — a bad
        /// batch is retried on the next pass rather than spun on here.
        /// </summary>
        private const int MaxBatchesPerPass = 40;

        /// <summary>Safety net for the day nobody logs in and no rollover is ever observed.</summary>
        private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

        /// <summary>
        /// How long to wait before retrying a row that failed, instead of leaving it for the next
        /// hourly sweep. The failures worth retrying are transient file locks — a backup agent or
        /// an antivirus scan holding a large .webm — which clear on their own in minutes.
        /// </summary>
        private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(3);

        /// <summary>
        /// Attempts a row gets before its failure is logged as an error. Below this the failure is
        /// silent, because a lock that clears on the second try is not something to wake anyone
        /// for; at this count it has survived ~12 minutes and is a real problem.
        /// </summary>
        private const int MaxAttempts = 5;

        /// <summary>
        /// Consecutive failed attempts per row, keyed by the "spec#id" identity the migration
        /// service puts at the front of every error. Only ever touched from the single pass loop,
        /// so it needs no synchronisation.
        /// </summary>
        private readonly Dictionary<string, int> _attempts = new();

        /// <summary>Let the app finish starting before touching S3 or the disk.</summary>
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

        private readonly ILogger<ScreenRecordS3UploadService> _logger;
        private readonly IServiceProvider _services;
        private readonly ScreenRecordFinalizeSignal _signal;

        public ScreenRecordS3UploadService(
            ILogger<ScreenRecordS3UploadService> logger,
            IServiceProvider services,
            ScreenRecordFinalizeSignal signal)
        {
            _logger = logger;
            _services = services;
            _signal = signal;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScreenRecordS3UploadService is starting.");

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                // Set when a row failed but has attempts left, which shortens the wait below.
                var retryPending = false;

                try
                {
                    // Runs once on startup before the first wait, so a backlog left by an earlier
                    // build is cleared without needing a rollover or a button press.
                    retryPending = await RunPassAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Never let one bad pass kill the service — the next one retries.
                    // A whole pass failing is not a per-row lock, so it keeps the hourly cadence
                    // rather than spinning every RetryInterval.
                    _logger.LogError(ex, "Screen recording S3 upload pass failed.");
                }

                try
                {
                    await _signal.WaitAsync(retryPending ? RetryInterval : SweepInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Drains every closed recording day that is not in the bucket yet, then deletes the local
        /// copy of anything that just verified in S3. Batches are cursor-paged by the migration
        /// service, so rows it cannot migrate do not stall the pass.
        ///
        /// Returns true when a row failed and still has attempts left, which tells the caller to
        /// come back in RetryInterval instead of SweepInterval.
        /// </summary>
        private async Task<bool> RunPassAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var migration = scope.ServiceProvider.GetRequiredService<MediaMigrationService>();

            Dictionary<string, int>? cursors = null;
            var errors = new List<string>();
            var migrated = 0;
            var failed = 0;
            var missing = 0;
            long bytes = 0;

            for (var batch = 0; batch < MaxBatchesPerPass; batch++)
            {
                ct.ThrowIfCancellationRequested();

                var result = await migration.MigrateModuleBatchAsync(
                    ModuleKey,
                    BatchSize,
                    cursors,
                    userId: null,
                    userName: nameof(ScreenRecordS3UploadService),
                    ct);

                migrated += result.Migrated;
                failed += result.FailedCount;
                missing += result.SkippedMissingFile;
                bytes += result.MigratedBytes;
                cursors = result.Cursors;

                // Not logged here: a failure is only worth reporting once it has survived
                // MaxAttempts, which TrackFailures decides after the whole pass is in.
                errors.AddRange(result.Errors);

                if (!result.HasMore)
                    break;
            }

            var (retryPending, reported) = TrackFailures(errors);

            // Silent on the common case — most passes find a day that is already uploaded — and
            // silent on failures still inside their retry budget, so a lock that clears on its
            // own never reaches the log at all.
            if ((migrated > 0 || missing > 0 || reported) && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Screen recordings finalized to S3: {Migrated} uploaded ({Megabytes:F1} MB), " +
                    "{Failed} failed, {Missing} skipped with no local file.",
                    migrated,
                    bytes / 1048576.0,
                    failed,
                    missing);
            }

            if (migrated > 0)
                await RunDeleteLocalPassAsync(migration, ct);

            return retryPending;
        }

        /// <summary>
        /// Counts consecutive failures per row and decides which ones the log hears about.
        ///
        /// A failed upload is retried by the next pass regardless — passes restart from a null
        /// cursor and the migration is idempotent, so a row that failed is simply re-examined.
        /// What this adds is patience: the first MaxAttempts-1 failures are silent and bring the
        /// next pass forward to RetryInterval, and only a row that keeps failing past that is
        /// logged as an error. Nothing is at risk while it retries, because a row with no S3
        /// object keeps its local file and delete-local will not touch it.
        ///
        /// Returns whether any row still has attempts left, and whether anything was reported.
        /// </summary>
        private (bool RetryPending, bool Reported) TrackFailures(List<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var retryPending = false;
            var reported = false;

            foreach (var error in errors)
            {
                var identity = IdentityOf(error);
                seen.Add(identity);

                var attempt = _attempts.TryGetValue(identity, out var previous) ? previous + 1 : 1;
                _attempts[identity] = attempt;

                if (attempt < MaxAttempts)
                {
                    retryPending = true;

                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug(
                            "Screen recording upload attempt {Attempt}/{Max} failed, retrying in {Minutes} min: {Error}",
                            attempt,
                            MaxAttempts,
                            RetryInterval.TotalMinutes,
                            error);
                }
                else if (attempt == MaxAttempts)
                {
                    // Reported once. Further failures fall through silently on the hourly sweep
                    // rather than repeating the same error every RetryInterval forever.
                    reported = true;

                    _logger.LogError(
                        "Screen recording upload error after {Attempts} attempts: {Error}",
                        attempt,
                        error);
                }
            }

            // A row that failed before and did not fail now has gone through, so its count is
            // dropped and a later failure starts fresh. Rows beyond this pass's reach would be
            // cleared the same way, but the pass covers MaxBatchesPerPass * BatchSize rows and
            // this module holds one row per employee per day, so that ceiling is not in play.
            foreach (var identity in _attempts.Keys.Where(k => !seen.Contains(k)).ToList())
                _attempts.Remove(identity);

            return (retryPending, reported);
        }

        /// <summary>
        /// The stable row identity the migration service prefixes every error with, as
        /// "EmployeeScreenRecord.VideoPath#23 — {message}". Keying on the whole string would
        /// restart the count whenever the message text varied.
        /// </summary>
        private static string IdentityOf(string error)
        {
            var separator = error.IndexOf('—');

            return separator < 0 ? error : error[..separator].TrimEnd();
        }

        /// <summary>
        /// Frees disk for whatever this pass (or an earlier one) uploaded. Reuses the dashboard's
        /// delete-local path unchanged: every candidate is HEAD-checked against the bucket and
        /// compared byte-for-byte before removal, so a file only leaves disk once its S3 copy is
        /// confirmed intact — an upload that failed or landed truncated is kept, not deleted.
        /// </summary>
        private async Task RunDeleteLocalPassAsync(MediaMigrationService migration, CancellationToken ct)
        {
            Dictionary<string, int>? cursors = null;
            var deleted = 0;
            var kept = 0;
            long bytes = 0;

            for (var batch = 0; batch < MaxBatchesPerPass; batch++)
            {
                ct.ThrowIfCancellationRequested();

                var result = await migration.DeleteLocalModuleBatchAsync(
                    ModuleKey,
                    BatchSize,
                    cursors,
                    confirm: true,
                    ct);

                deleted += result.Deleted;
                kept += result.KeptNotInBucket + result.KeptSizeMismatch;
                bytes += result.DeletedBytes;
                cursors = result.Cursors;

                foreach (var error in result.Errors)
                    _logger.LogError("Screen recording local delete error: {Error}", error);

                if (!result.HasMore)
                    break;
            }

            if ((deleted > 0 || kept > 0) && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Screen recordings local cleanup: {Deleted} deleted ({Megabytes:F1} MB), {Kept} kept pending verification.",
                    deleted,
                    bytes / 1048576.0,
                    kept);
            }
        }
    }
