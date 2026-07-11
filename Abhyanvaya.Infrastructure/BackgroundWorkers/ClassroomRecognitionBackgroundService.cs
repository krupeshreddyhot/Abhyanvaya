using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Constants;
using Abhyanvaya.Infrastructure.Diagnostics;
using Abhyanvaya.Infrastructure.InsightFace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>Processes classroom photo upload jobs via <see cref="IClassroomRecognitionPipeline"/>.</summary>
public sealed class ClassroomRecognitionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClassroomPhotoQueue _queue;
    private readonly IOptions<InsightFaceOptions> _insightFaceOptions;
    private readonly ILogger<ClassroomRecognitionBackgroundService> _logger;

    // AI15.DIAGNOSTICS.2C: always 1 today — no retry mechanism exists yet. Named here (rather than a
    // bare literal at the call site) so the "diagnostics-only groundwork for future retries" intent is
    // visible at the point the value is produced.
    private const int CurrentRecognitionAttempt = 1;

    public ClassroomRecognitionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IClassroomPhotoQueue queue,
        IOptions<InsightFaceOptions> insightFaceOptions,
        ILogger<ClassroomRecognitionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _insightFaceOptions = insightFaceOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Classroom recognition background worker started.");

        // AI15.DIAGNOSTICS.2A: measures how long the loop waited for the *next* item, restarted at
        // the end of each iteration. Diagnostics-only — does not affect dequeue behavior.
        var waitStopwatch = Stopwatch.StartNew();

        await foreach (var message in _queue.DequeueAllAsync(stoppingToken))
        {
            var elapsedSinceWaitingMs = waitStopwatch.ElapsedMilliseconds;
            var queueUtc = DateTime.UtcNow;

            try
            {
                _logger.LogInformation(
                    "Classroom recognition job dequeued. SessionId={SessionId} TenantId={TenantId} QueueDepth={QueueDepth}",
                    message.AttendanceSessionId,
                    message.TenantId,
                    _queue.Count);

                await using var scope = _scopeFactory.CreateAsyncScope();

                // Establish the ambient tenant for this scope so EF Core global query filters
                // resolve exactly as they do for an authenticated HTTP request. The pipeline stays
                // unaware of tenancy; it simply queries and the filters apply the correct tenant.
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
                tenantContext.SetTenant(message.TenantId);

                // AI15.DIAGNOSTICS.2A: one IRecognitionExecutionContext per job — Scoped, bound to
                // this DI scope, cleared in `finally` below. Not static/AsyncLocal/ThreadStatic.
                var executionContext = scope.ServiceProvider.GetRequiredService<IRecognitionExecutionContext>();
                executionContext.Initialize(message.AttendanceSessionId, message.TenantId, CurrentRecognitionAttempt, queueUtc);

                LogQueueTrace(message, elapsedSinceWaitingMs, executionContext);

                try
                {
                    var pipeline = scope.ServiceProvider.GetRequiredService<IClassroomRecognitionPipeline>();

                    _logger.LogInformation("ENTERING PIPELINE. SessionId={SessionId} TenantId={TenantId}", message.AttendanceSessionId, message.TenantId);
                    var pipelineStopwatch = Stopwatch.StartNew();

                    try
                    {
                        await pipeline.ProcessAsync(message, stoppingToken);
                        _logger.LogInformation(
                            "PIPELINE COMPLETED. SessionId={SessionId} TenantId={TenantId} ElapsedMs={ElapsedMs}",
                            message.AttendanceSessionId,
                            message.TenantId,
                            pipelineStopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception pipelineEx)
                    {
                        // Diagnostics-only: logs then rethrows unchanged — the existing outer catch
                        // below still owns the actual failure handling/behavior.
                        _logger.LogError(
                            pipelineEx,
                            "PIPELINE FAILED. SessionId={SessionId} TenantId={TenantId} ExceptionType={ExceptionType} ElapsedMs={ElapsedMs}",
                            message.AttendanceSessionId,
                            message.TenantId,
                            pipelineEx.GetType().Name,
                            pipelineStopwatch.ElapsedMilliseconds);
                        throw;
                    }
                }
                finally
                {
                    // Always clear the tenant so no state leaks across jobs, even on failure.
                    tenantContext.Clear();
                    executionContext.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Classroom recognition job failed. SessionId={SessionId} TenantId={TenantId}",
                    message.AttendanceSessionId,
                    message.TenantId);
            }
            finally
            {
                waitStopwatch.Restart();
            }
        }
    }

    // AI15.DIAGNOSTICS.2A/2B/2C: read-only snapshot + logging around the just-dequeued message; does
    // not touch the message, the queue, or any recognition/matching/persistence decision.
    private void LogQueueTrace(ClassroomPhotoMessage message, long elapsedSinceWaitingMs, IRecognitionExecutionContext executionContext)
    {
        try
        {
            var snapshot = RecognitionMemorySnapshot.Capture();

            _logger.LogInformation("====================================================");
            _logger.LogInformation("QUEUE TRACE");
            _logger.LogInformation("  Queue Item Received");
            _logger.LogInformation("  Attendance Session Id              : {AttendanceSessionId}", message.AttendanceSessionId);
            _logger.LogInformation("  Tenant Id                          : {TenantId}", message.TenantId);
            _logger.LogInformation("  Storage Key                        : {StorageKey}", message.ImageStorageKey);
            _logger.LogInformation("  Queue Depth                        : {QueueDepth}", _queue.Count);
            _logger.LogInformation("  UTC Timestamp                      : {TimestampUtc:O}", snapshot.TimestampUtc);
            _logger.LogInformation("  Elapsed Since Waiting              : {ElapsedSinceWaitingMs} ms", elapsedSinceWaitingMs);
            _logger.LogInformation("  Thread Id                          : {ThreadId}", snapshot.ThreadId);
            _logger.LogInformation("====================================================");

            ExecutionTraceLog.LogBlock(_logger, executionContext, _insightFaceOptions.Value.PipelineVersion, EmbeddingProviders.InsightFace);
        }
        catch (Exception ex)
        {
            // Diagnostics-only: a logging failure here must never prevent the job from processing.
            _logger.LogWarning(ex, "Queue trace diagnostics logging failed; continuing without it.");
        }
    }
}
