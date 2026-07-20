using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;

public sealed class ProgressEnrollmentPipelineStage : IEnrollmentPipelineStage
{
    private readonly IEnrollmentProgressReporter _progressReporter;

    public ProgressEnrollmentPipelineStage(IEnrollmentProgressReporter progressReporter)
    {
        _progressReporter = progressReporter;
    }

    public EnrollmentPipelineStage? ManifestStage => EnrollmentPipelineStage.Finalize;
    public string Name => "Progress";
    public int Order => 400;
    public string Description => "Finalize enrollment progress reporting and batch completion checks.";
    public string Version => "1.0";
    public bool SupportsRetry => false;
    public bool SupportsResume => false;

    public async Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var itemContext = context.ItemContext;

        if (context.PersistenceResult?.Success == true
            && context.Request.ItemStatus == EnrollmentStatus.Embedding)
        {
            var embeddingProgress = await _progressReporter.MarkStageCompletedAsync(
                new EnrollmentStageProgressRequest
                {
                    ItemId = itemContext.ItemId,
                    BatchId = itemContext.BatchId,
                    TenantId = itemContext.TenantId,
                    ExpectedStatus = EnrollmentStatus.Embedding,
                    Stage = EnrollmentPipelineStage.Embedding,
                    CorrelationId = itemContext.CorrelationId,
                    ExecutionTraceId = itemContext.ExecutionTraceId,
                    PipelineVersion = itemContext.PipelineVersion,
                },
                cancellationToken);

            if (!embeddingProgress.Applied && !embeddingProgress.ConcurrencyConflict)
            {
                stopwatch.Stop();
                return EnrollmentPipelineStageExecutionResult.Failed(
                    context with { State = EnrollmentPipelineState.Failed },
                    stopwatch.Elapsed,
                    EnrollmentPipelineFailureCodes.ProgressConflict,
                    embeddingProgress.Reason ?? "Unable to finalize item progress.");
            }
        }
        else if (context.StorageResult?.Success == true
                 && !context.EmbeddingEligible
                 && context.Request.ItemStatus == EnrollmentStatus.Embedding)
        {
            var photoOnlyCompletion = await _progressReporter.MarkItemCompletedAsync(
                new EnrollmentProgressOperationRequest
                {
                    ItemId = itemContext.ItemId,
                    BatchId = itemContext.BatchId,
                    TenantId = itemContext.TenantId,
                    ExpectedStatus = EnrollmentStatus.Embedding,
                    CorrelationId = itemContext.CorrelationId,
                    ExecutionTraceId = itemContext.ExecutionTraceId,
                    PipelineVersion = itemContext.PipelineVersion,
                },
                cancellationToken);

            if (!photoOnlyCompletion.Applied && !photoOnlyCompletion.ConcurrencyConflict)
            {
                stopwatch.Stop();
                return EnrollmentPipelineStageExecutionResult.Failed(
                    context with { State = EnrollmentPipelineState.Failed },
                    stopwatch.Elapsed,
                    EnrollmentPipelineFailureCodes.ProgressConflict,
                    photoOnlyCompletion.Reason ?? "Unable to finalize photo-only enrollment.");
            }
        }

        await _progressReporter.UpdateProgressAsync(
            itemContext.BatchId,
            itemContext.TenantId,
            cancellationToken);

        await _progressReporter.FinalizeBatchIfCompleteAsync(
            itemContext.BatchId,
            itemContext.TenantId,
            cancellationToken);

        stopwatch.Stop();
        return EnrollmentPipelineStageExecutionResult.Succeeded(
            context with
            {
                State = EnrollmentPipelineState.Completed,
                CurrentStage = EnrollmentPipelineStage.Finalize,
                Request = context.Request with { ItemStatus = EnrollmentStatus.Completed },
            },
            stopwatch.Elapsed);
    }
}
