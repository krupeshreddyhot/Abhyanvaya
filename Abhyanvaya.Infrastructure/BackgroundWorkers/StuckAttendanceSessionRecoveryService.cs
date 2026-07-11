using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>
/// Periodically reconciles <see cref="AttendanceSession"/> rows that were left in
/// <see cref="AttendanceSessionStatus.Processing"/> by a job that will never complete.
/// </summary>
/// <remarks>
/// The classroom recognition queue (<see cref="IClassroomPhotoQueue"/>) is an in-process,
/// non-durable channel. If the host process crashes or is forcibly restarted mid-job (for example,
/// after Render's out-of-memory watchdog kills the container), the in-flight message is lost, but
/// the session row was already committed as <see cref="AttendanceSessionStatus.Processing"/>. With
/// no worker left holding that job, and no timeout in <c>ClassroomRecognitionPipeline</c> itself,
/// the row — and the UI progress bar backed by it — would otherwise stay "stuck" forever.
/// This sweep runs across all tenants (it deliberately bypasses the tenant query filter) so a single
/// instance can recover every orphaned session, then moves each one to
/// <see cref="AttendanceSessionStatus.Failed"/> with a clear, retryable error message.
///
/// AI14.RUNTIME.2 (throttling): each run recovers at most
/// <see cref="AttendanceSessionRecoveryOptions.MaxRecoveriesPerRun"/> sessions — oldest first — and
/// leaves any remainder for the next scan instead of processing an unbounded number of rows in one
/// pass. AI14.RUNTIME.3 (structured logging): every session actually recovered is logged with
/// tenant, session id, start time, and how long it sat orphaned, plus a single end-of-run summary —
/// no student data or face images ever appear in these logs.
/// </remarks>
public sealed class StuckAttendanceSessionRecoveryService : BackgroundService
{
    private const string RecoveryReason = "Worker terminated unexpectedly";
    private const string RecoverySource = "Automatic Recovery Service";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClassroomPhotoQueue _queue;
    private readonly IAttendanceSessionRecoveryMetrics _metrics;
    private readonly AttendanceSessionRecoveryOptions _options;
    private readonly ILogger<StuckAttendanceSessionRecoveryService> _logger;

    public StuckAttendanceSessionRecoveryService(
        IServiceScopeFactory scopeFactory,
        IClassroomPhotoQueue queue,
        IAttendanceSessionRecoveryMetrics metrics,
        IOptions<AttendanceSessionRecoveryOptions> options,
        ILogger<StuckAttendanceSessionRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Attendance session recovery sweep is disabled (AttendanceSessionRecovery:Enabled=false). Orphaned sessions will not be auto-recovered.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(5, _options.ScanIntervalSeconds));
        _logger.LogInformation(
            "Attendance session recovery sweep started. TimeoutMinutes={TimeoutMinutes} ScanIntervalSeconds={ScanIntervalSeconds} MaxRecoveriesPerRun={MaxRecoveriesPerRun}",
            _options.TimeoutMinutes,
            pollInterval.TotalSeconds,
            _options.MaxRecoveriesPerRun);

        using var timer = new PeriodicTimer(pollInterval);

        // Run once immediately on startup to recover anything orphaned by the previous crash/restart,
        // then continue on the configured interval.
        do
        {
            try
            {
                await RecoverStuckSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attendance session recovery sweep failed unexpectedly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RecoverStuckSessionsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var cutoffUtc = DateTime.UtcNow.AddMinutes(-_options.TimeoutMinutes);

        // IgnoreQueryFilters: this maintenance sweep must see every tenant's orphaned sessions, not
        // just the tenant of whichever request happened to create this scope (there is none here).
        var stuckQuery = context.AttendanceSessions
            .IgnoreQueryFilters()
            .Where(s => s.Status == AttendanceSessionStatus.Processing
                        && s.StartedUtc != null
                        && s.StartedUtc < cutoffUtc);

        var candidatesFound = await stuckQuery.CountAsync(cancellationToken);

        if (candidatesFound == 0)
        {
            LogSweepCompleted(candidatesFound, recovered: 0, remaining: 0, stopwatch.Elapsed);
            _metrics.RecordRun(recoveredCount: 0, pendingRemaining: 0, (long)stopwatch.Elapsed.TotalMilliseconds);
            return;
        }

        // AI14.RUNTIME.2: recover at most MaxRecoveriesPerRun sessions per run, oldest-first, so a
        // single sweep never processes an unbounded number of rows; anything beyond the cap is left
        // for the next scan.
        var batch = await stuckQuery
            .OrderBy(s => s.StartedUtc)
            .Take(Math.Max(0, _options.MaxRecoveriesPerRun))
            .ToListAsync(cancellationToken);

        var recoveredCount = 0;
        foreach (var session in batch)
        {
            if (await TryRecoverSessionAsync(session, unitOfWork, cancellationToken))
            {
                recoveredCount++;
            }
        }

        var remaining = Math.Max(0, candidatesFound - batch.Count);
        LogSweepCompleted(candidatesFound, recoveredCount, remaining, stopwatch.Elapsed);
        _metrics.RecordRun(recoveredCount, remaining, (long)stopwatch.Elapsed.TotalMilliseconds);
    }

    private async Task<bool> TryRecoverSessionAsync(
        AttendanceSession session,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        try
        {
            var startedUtc = session.StartedUtc!.Value;
            var nowUtc = DateTime.UtcNow;

            session.ProcessingError =
                "Recognition processing did not complete within the expected time and was reset. " +
                "This usually means the server process was interrupted or restarted while this " +
                "photo was being processed (e.g. after a resource limit was exceeded). Please retry " +
                "the upload.";
            session.CompletedUtc = nowUtc;
            session.MoveToFailed();

            // Save per-session (not batched) so a concurrency conflict on one row — e.g. the
            // worker that owns it actually finished between our query and this write — cannot
            // block recovery of the rest of the batch, and cannot overwrite that legitimate
            // completion.
            await ConcurrencyExceptionHelper.SaveChangesAsync(unitOfWork, cancellationToken);
            _queue.MarkCompleted(session.Id);

            LogSessionRecovered(session.TenantId, session.Id, startedUtc, nowUtc - startedUtc);
            return true;
        }
        catch (Exception ex)
        {
            // Never let one bad row abort the sweep for the rest of the batch.
            _logger.LogError(ex, "Failed to recover stuck attendance session. SessionId={SessionId}", session.Id);
            return false;
        }
    }

    // AI14.RUNTIME.3: structured, per-session recovery log. Deliberately carries no student data or
    // face imagery — only the tenant id, session id, timestamps, and fixed reason/source labels.
    private void LogSessionRecovered(int tenantId, Guid sessionId, DateTime startedUtc, TimeSpan orphanedFor)
    {
        var recoveredAfterMinutes = Math.Max(0, (int)Math.Round(orphanedFor.TotalMinutes));

        _logger.LogWarning("Attendance Session Recovery");
        _logger.LogWarning("  Tenant                              : {TenantId}", tenantId);
        _logger.LogWarning("  Session                             : {SessionId}", sessionId);
        _logger.LogWarning("  Started                             : {StartedUtc:yyyy-MM-dd HH:mm} UTC", startedUtc);
        _logger.LogWarning("  Recovered After                     : {RecoveredAfterMinutes} minutes", recoveredAfterMinutes);
        _logger.LogWarning("  Recovery Reason                     : {RecoveryReason}", RecoveryReason);
        _logger.LogWarning("  Recovery Source                     : {RecoverySource}", RecoverySource);
    }

    // AI14.RUNTIME.2 + AI14.RUNTIME.3: single end-of-run summary combining the throttling visibility
    // (candidates found / recovered / remaining) with the run duration, instead of emitting two
    // near-identical summary blocks for the same sweep execution.
    private void LogSweepCompleted(int candidatesFound, int recovered, int remaining, TimeSpan duration)
    {
        _logger.LogInformation("Recovery Sweep Completed");
        _logger.LogInformation("  Candidates Found                    : {CandidatesFound}", candidatesFound);
        _logger.LogInformation("  Recovered                           : {Recovered}", recovered);
        _logger.LogInformation("  Remaining                           : {Remaining}", remaining);
        _logger.LogInformation("  Duration                             : {DurationMs} ms", (int)duration.TotalMilliseconds);
    }
}
