using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration;

public sealed class EnrollmentPipelineExecutor : IEnrollmentPipelineExecutor
{
    private readonly IEnrollmentPipelineRegistry _registry;
    private readonly IEnrollmentRetryPolicy _retryPolicy;
    private readonly IEnrollmentPipelineMetrics _metrics;
    private readonly ILogger<EnrollmentPipelineExecutor> _logger;

    public EnrollmentPipelineExecutor(
        IEnrollmentPipelineRegistry registry,
        IEnrollmentRetryPolicy retryPolicy,
        IEnrollmentPipelineMetrics metrics,
        ILogger<EnrollmentPipelineExecutor> logger)
    {
        _registry = registry;
        _retryPolicy = retryPolicy;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<EnrollmentPipelineResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var pipelineStopwatch = Stopwatch.StartNew();
        var itemContext = context.ItemContext;
        var stageResults = new List<EnrollmentPipelineStageOutcome>();
        var stageDurations = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        var totalRetryCount = 0;
        var failureCount = 0;
        var currentContext = context;

        _metrics.RecordPipelineStarted(itemContext.CorrelationId, itemContext.PipelineVersion);

        _ = new PipelineStarted(
            itemContext.ItemId,
            itemContext.BatchId,
            itemContext.StudentId,
            itemContext.CorrelationId,
            itemContext.PipelineVersion,
            DateTime.UtcNow);

        _logger.LogInformation(
            "Enrollment pipeline started. ItemId={ItemId} BatchId={BatchId} StudentId={StudentId} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion}",
            itemContext.ItemId,
            itemContext.BatchId,
            itemContext.StudentId,
            itemContext.CorrelationId,
            itemContext.PipelineVersion);

        var stages = _registry.GetOrderedStages(itemContext.PipelineVersion);

        foreach (var stage in stages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            currentContext = currentContext with
            {
                CurrentStage = stage.ManifestStage,
                Request = currentContext.Request with
                {
                    Context = currentContext.Request.Context with
                    {
                        CurrentStage = stage.ManifestStage,
                        PipelineState = currentContext.State,
                    },
                },
            };

            _logger.LogInformation(
                "Enrollment pipeline stage started. Stage={StageName} ItemId={ItemId} CorrelationId={CorrelationId}",
                stage.Name,
                itemContext.ItemId,
                itemContext.CorrelationId);

            var attemptCount = 0;
            EnrollmentPipelineStageExecutionResult stageResult;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attemptCount++;

                stageResult = await stage.ExecuteAsync(currentContext, cancellationToken);

                if (stageResult.Success || stageResult.IsCancelled)
                {
                    break;
                }

                var retryDecision = _retryPolicy.Evaluate(stage, stageResult, attemptCount);
                if (!retryDecision.ShouldRetry)
                {
                    break;
                }

                totalRetryCount++;
                _metrics.RecordStageRetry(stage.Name, attemptCount);

                if (retryDecision.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(retryDecision.Delay, cancellationToken);
                }
            }

            stageDurations[stage.Name] = stageResult.Duration;
            stageResults.Add(new EnrollmentPipelineStageOutcome
            {
                ManifestStage = stage.ManifestStage,
                StageName = stage.Name,
                Success = stageResult.Success,
                Duration = stageResult.Duration,
                FailureCode = stageResult.FailureCode,
                FailureReason = stageResult.FailureReason,
                FailureCategory = stageResult.FailureCategory,
                RetryAttempts = stageResult.RetryAttempts + Math.Max(0, attemptCount - 1),
            });

            _metrics.RecordStageCompleted(stage.Name, (long)stageResult.Duration.TotalMilliseconds, stageResult.Success);
            currentContext = stageResult.Context with { TotalRetryCount = totalRetryCount };

            if (stageResult.Success)
            {
                _ = new PipelineStageCompleted(
                    itemContext.ItemId,
                    itemContext.BatchId,
                    itemContext.StudentId,
                    itemContext.CorrelationId,
                    stage.Name,
                    (long)stageResult.Duration.TotalMilliseconds,
                    DateTime.UtcNow);

                _logger.LogInformation(
                    "Enrollment pipeline stage completed. Stage={StageName} ItemId={ItemId} CorrelationId={CorrelationId} DurationMs={DurationMs}",
                    stage.Name,
                    itemContext.ItemId,
                    itemContext.CorrelationId,
                    stageResult.Duration.TotalMilliseconds);

                continue;
            }

            failureCount++;
            pipelineStopwatch.Stop();

            if (stageResult.IsCancelled)
            {
                _metrics.RecordPipelineCancelled(itemContext.CorrelationId);

                _ = new PipelineCancelled(
                    itemContext.ItemId,
                    itemContext.BatchId,
                    itemContext.StudentId,
                    itemContext.CorrelationId,
                    stage.Name,
                    DateTime.UtcNow);

                _logger.LogWarning(
                    "Enrollment pipeline cancelled. Stage={StageName} ItemId={ItemId} CorrelationId={CorrelationId} DurationMs={DurationMs}",
                    stage.Name,
                    itemContext.ItemId,
                    itemContext.CorrelationId,
                    pipelineStopwatch.ElapsedMilliseconds);

                return EnrollmentPipelineResult.Failed(
                    currentContext.Request,
                    stage.ManifestStage,
                    EnrollmentPipelineState.Cancelled,
                    pipelineStopwatch.Elapsed,
                    stageResults,
                    BuildStatistics(pipelineStopwatch.Elapsed, stageDurations, totalRetryCount, failureCount, wasCancelled: true, currentContext.Warnings),
                    EnrollmentPipelineFailureCodes.Cancelled,
                    stageResult.FailureReason ?? "Pipeline cancelled.");
            }

            _ = new PipelineFailed(
                itemContext.ItemId,
                itemContext.BatchId,
                itemContext.StudentId,
                itemContext.CorrelationId,
                stage.Name,
                stageResult.FailureCode ?? EnrollmentPipelineFailureCodes.StageFailed,
                stageResult.FailureReason ?? "Stage failed.",
                DateTime.UtcNow);

            _logger.LogWarning(
                "Enrollment pipeline stage failed. Stage={StageName} ItemId={ItemId} CorrelationId={CorrelationId} FailureCode={FailureCode} DurationMs={DurationMs} Reason={Reason}",
                stage.Name,
                itemContext.ItemId,
                itemContext.CorrelationId,
                stageResult.FailureCode,
                pipelineStopwatch.ElapsedMilliseconds,
                stageResult.FailureReason);

            _metrics.RecordPipelineCompleted(itemContext.CorrelationId, pipelineStopwatch.ElapsedMilliseconds, success: false);

            return EnrollmentPipelineResult.Failed(
                currentContext.Request,
                stage.ManifestStage,
                EnrollmentPipelineState.Failed,
                pipelineStopwatch.Elapsed,
                stageResults,
                BuildStatistics(pipelineStopwatch.Elapsed, stageDurations, totalRetryCount, failureCount, wasCancelled: false, currentContext.Warnings),
                stageResult.FailureCode ?? EnrollmentPipelineFailureCodes.StageFailed,
                stageResult.FailureReason ?? "Stage failed.",
                stageResult.FailureCategory);
        }

        pipelineStopwatch.Stop();
        _metrics.RecordPipelineCompleted(itemContext.CorrelationId, pipelineStopwatch.ElapsedMilliseconds, success: true);

        _ = new PipelineCompleted(
            itemContext.ItemId,
            itemContext.BatchId,
            itemContext.StudentId,
            itemContext.CorrelationId,
            pipelineStopwatch.ElapsedMilliseconds,
            DateTime.UtcNow);

        _logger.LogInformation(
            "Enrollment pipeline completed. ItemId={ItemId} BatchId={BatchId} StudentId={StudentId} CorrelationId={CorrelationId} DurationMs={DurationMs}",
            itemContext.ItemId,
            itemContext.BatchId,
            itemContext.StudentId,
            itemContext.CorrelationId,
            pipelineStopwatch.ElapsedMilliseconds);

        return EnrollmentPipelineResult.Succeeded(
            currentContext.Request,
            EnrollmentPipelineState.Completed,
            EnrollmentPipelineStage.Finalize,
            currentContext.Request.ItemStatus,
            pipelineStopwatch.Elapsed,
            stageResults,
            BuildStatistics(pipelineStopwatch.Elapsed, stageDurations, totalRetryCount, failureCount, wasCancelled: false, currentContext.Warnings),
            currentContext.Warnings.Count > 0 ? currentContext.Warnings : null,
            currentContext.PersistenceResult);
    }

    private static EnrollmentPipelineStatistics BuildStatistics(
        TimeSpan totalDuration,
        IReadOnlyDictionary<string, TimeSpan> stageDurations,
        int retryCount,
        int failureCount,
        bool wasCancelled,
        IReadOnlyList<string> warnings) =>
        new()
        {
            TotalDuration = totalDuration,
            StageDurations = stageDurations,
            RetryCount = retryCount,
            FailureCount = failureCount,
            WasCancelled = wasCancelled,
            Warnings = warnings.Count > 0 ? warnings : null,
        };
}
