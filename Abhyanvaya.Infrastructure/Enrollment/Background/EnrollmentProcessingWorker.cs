using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentProcessingWorker : IEnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnrollmentWorkScheduler _scheduler;
    private readonly IEnrollmentWorkerMetrics _metrics;
    private readonly EnrollmentBackgroundOptions _options;
    private readonly string _workerId;
    private readonly string _nodeId;
    private readonly ILogger<EnrollmentProcessingWorker> _logger;

    public EnrollmentProcessingWorker(
        IServiceScopeFactory scopeFactory,
        IEnrollmentWorkScheduler scheduler,
        IEnrollmentWorkerMetrics metrics,
        IOptions<EnrollmentBackgroundOptions> options,
        ILogger<EnrollmentProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _scheduler = scheduler;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
        _workerId = EnrollmentWorkerIdentity.CreateWorkerId();
        _nodeId = EnrollmentWorkerIdentity.NodeId;
    }

    public string WorkerId => _workerId;

    public async Task<EnrollmentWorkerResult?> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var workItem = await _scheduler.GetNextWorkAsync(cancellationToken);
        if (workItem == null)
        {
            return null;
        }

        return await ProcessWorkItemAsync(workItem, cancellationToken);
    }

    internal async Task<EnrollmentWorkerResult> ProcessWorkItemAsync(
        EnrollmentWorkItem workItem,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _metrics.RecordWorkerStarted(_workerId);
        _ = new WorkerStarted(_workerId, _nodeId, DateTime.UtcNow);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant(workItem.TenantId);

        var leaseManager = scope.ServiceProvider.GetRequiredService<IEnrollmentLeaseManager>();
        var heartbeatService = scope.ServiceProvider.GetRequiredService<IEnrollmentHeartbeatService>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IEnrollmentOrchestrator>();
        var workScheduler = scope.ServiceProvider.GetRequiredService<IEnrollmentWorkScheduler>();

        var lease = await leaseManager.AcquireAsync(workItem, _workerId, _nodeId, cancellationToken);
        if (lease == null)
        {
            stopwatch.Stop();
            return new EnrollmentWorkerResult
            {
                EnrollmentId = workItem.ItemId,
                WorkerId = _workerId,
                LeaseId = Guid.Empty,
                Duration = stopwatch.Elapsed,
                Success = false,
                FailureCode = "worker.lease_conflict",
                FailureReason = "Unable to acquire lease for work item.",
            };
        }

        _metrics.RecordLeaseAcquired(lease.LeaseId);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = RunHeartbeatLoopAsync(leaseManager, heartbeatService, lease, heartbeatCts.Token);

        EnrollmentPipelineResult pipelineResult;
        try
        {
            _logger.LogInformation(
                "Pipeline started by worker. ItemId={ItemId} WorkerId={WorkerId} CorrelationId={CorrelationId}",
                workItem.ItemId,
                _workerId,
                workItem.CorrelationId);

            pipelineResult = await orchestrator.ProcessItemAsync(BuildPipelineRequest(workItem), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = new WorkerFailed(workItem.ItemId, _workerId, "worker.unexpected_failure", ex.Message);

            await workScheduler.ScheduleRetryAsync(new EnrollmentRetryScheduleRequest
            {
                WorkItem = workItem,
                StageName = "Worker",
                FailureCode = "worker.unexpected_failure",
                FailureReason = ex.Message,
                AttemptCount = workItem.RetryCount + 1,
            }, CancellationToken.None);

            stopwatch.Stop();
            await leaseManager.ReleaseAsync(lease, CancellationToken.None);
            _metrics.RecordLeaseReleased(lease.LeaseId);
            _metrics.RecordWorkerCompleted(_workerId, stopwatch.ElapsedMilliseconds, success: false);

            return new EnrollmentWorkerResult
            {
                EnrollmentId = workItem.ItemId,
                WorkerId = _workerId,
                LeaseId = lease.LeaseId,
                Duration = stopwatch.Elapsed,
                Success = false,
                FailureCode = "worker.unexpected_failure",
                FailureReason = ex.Message,
            };
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (!pipelineResult.Success)
        {
            await workScheduler.ScheduleRetryAsync(new EnrollmentRetryScheduleRequest
            {
                WorkItem = workItem,
                StageName = pipelineResult.CompletedStage?.ToString() ?? "Pipeline",
                FailureCode = pipelineResult.FailureCode,
                FailureReason = pipelineResult.FailureReason,
                FailureCategory = pipelineResult.FailureCategory,
                AttemptCount = workItem.RetryCount + 1,
            }, cancellationToken);
        }

        await leaseManager.ReleaseAsync(lease, cancellationToken);
        _metrics.RecordLeaseReleased(lease.LeaseId);

        stopwatch.Stop();
        _metrics.RecordWorkerCompleted(_workerId, stopwatch.ElapsedMilliseconds, pipelineResult.Success);
        _ = new WorkerCompleted(workItem.ItemId, _workerId, stopwatch.ElapsedMilliseconds, pipelineResult.Success);

        _logger.LogInformation(
            "Pipeline completed by worker. ItemId={ItemId} WorkerId={WorkerId} Success={Success} DurationMs={DurationMs}",
            workItem.ItemId,
            _workerId,
            pipelineResult.Success,
            stopwatch.ElapsedMilliseconds);

        return new EnrollmentWorkerResult
        {
            EnrollmentId = workItem.ItemId,
            WorkerId = _workerId,
            LeaseId = lease.LeaseId,
            Duration = stopwatch.Elapsed,
            PipelineResult = pipelineResult,
            Retries = workItem.RetryCount,
            Warnings = pipelineResult.Warnings,
            Success = pipelineResult.Success,
            FailureCode = pipelineResult.FailureCode,
            FailureReason = pipelineResult.FailureReason,
            Statistics = new EnrollmentWorkerStatistics
            {
                ItemsProcessed = 1,
                LeaseDuration = stopwatch.Elapsed,
                RetryCount = workItem.RetryCount,
                FailureCount = pipelineResult.Success ? 0 : 1,
                AveragePipelineDuration = pipelineResult.Duration,
            },
        };
    }

    private async Task RunHeartbeatLoopAsync(
        IEnrollmentLeaseManager leaseManager,
        IEnrollmentHeartbeatService heartbeatService,
        EnrollmentLease lease,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await heartbeatService.UpdateAsync(lease, EnrollmentWorkerState.Running, cancellationToken);
            await leaseManager.RenewAsync(lease, cancellationToken);
        }
    }

    private static EnrollmentPipelineRequest BuildPipelineRequest(EnrollmentWorkItem workItem) =>
        new()
        {
            Context = new EnrollmentItemContext
            {
                BatchId = workItem.BatchId,
                ItemId = workItem.ItemId,
                TenantId = workItem.TenantId,
                StudentId = workItem.StudentId,
                StudentNumber = workItem.StudentNumber,
                CollegeCode = workItem.CollegeCode,
                CollegeId = workItem.CollegeId,
                AcademicYear = workItem.AcademicYear,
                PhotoProviderName = workItem.PhotoProviderName,
                ExecutionTraceId = Guid.NewGuid(),
                CorrelationId = workItem.CorrelationId,
                PipelineVersion = workItem.PipelineVersion,
            },
            ItemStatus = workItem.Status,
            SourceUrl = workItem.SourceUrl,
            ContentType = workItem.ContentType,
            ByteSize = workItem.ByteSize,
        };
}
