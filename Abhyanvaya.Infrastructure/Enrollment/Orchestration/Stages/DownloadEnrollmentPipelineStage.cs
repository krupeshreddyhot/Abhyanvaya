using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;

public sealed class DownloadEnrollmentPipelineStage : IEnrollmentPipelineStage
{
    private readonly IStudentPhotoProviderFactory _photoProviderFactory;
    private readonly IEnrollmentProgressReporter _progressReporter;

    public DownloadEnrollmentPipelineStage(
        IStudentPhotoProviderFactory photoProviderFactory,
        IEnrollmentProgressReporter progressReporter)
    {
        _photoProviderFactory = photoProviderFactory;
        _progressReporter = progressReporter;
    }

    public EnrollmentPipelineStage? ManifestStage => EnrollmentPipelineStage.Download;
    public string Name => "Download";
    public int Order => 0;
    public string Description => "Fetch the enrollment reference photo from the configured provider.";
    public string Version => "1.0";
    public bool SupportsRetry => true;
    public bool SupportsResume => false;

    public async Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var itemContext = context.ItemContext;

        if (context.PhotoBytes is { Length: > 0 })
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Succeeded(
                context with { State = EnrollmentPipelineState.Pending },
                stopwatch.Elapsed);
        }

        var startTransition = await _progressReporter.MarkItemStartedAsync(
            CreateOperationRequest(context, context.Request.ItemStatus),
            cancellationToken);

        if (!startTransition.Applied)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.ProgressConflict,
                startTransition.Reason ?? "Unable to mark item as started.",
                isRetryable: startTransition.ConcurrencyConflict);
        }

        var provider = _photoProviderFactory.GetProvider(itemContext.PhotoProviderName);
        var fetchResult = await provider.FetchPhotoAsync(
            new StudentPhotoFetchRequest(
                itemContext.TenantId,
                itemContext.StudentId,
                itemContext.StudentNumber,
                itemContext.CollegeCode,
                itemContext.AcademicYear),
            cancellationToken);

        if (!fetchResult.Success || fetchResult.PhotoBytes is not { Length: > 0 })
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.StageFailed,
                fetchResult.FailureReason ?? "Photo download failed.",
                fetchResult.FailureCategory,
                isRetryable: fetchResult.FailureCategory is null
                    or FailureCategory.StorageUploadFailed
                    or FailureCategory.EmbeddingEngineFailed);
        }

        var completeTransition = await _progressReporter.MarkStageCompletedAsync(
            CreateStageRequest(context, EnrollmentStatus.Downloading, EnrollmentPipelineStage.Download),
            cancellationToken);

        if (!completeTransition.Applied)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.ProgressConflict,
                completeTransition.Reason ?? "Unable to mark download complete.",
                isRetryable: completeTransition.ConcurrencyConflict);
        }

        stopwatch.Stop();
        return EnrollmentPipelineStageExecutionResult.Succeeded(
            context with
            {
                State = EnrollmentPipelineState.Pending,
                PhotoBytes = fetchResult.PhotoBytes,
                ContentType = fetchResult.ContentType,
                PhotoByteSize = fetchResult.PhotoBytes.LongLength,
                Request = context.Request with
                {
                    ItemStatus = EnrollmentStatus.Downloaded,
                    PhotoBytes = fetchResult.PhotoBytes,
                    ContentType = fetchResult.ContentType,
                    ByteSize = (int)Math.Min(fetchResult.PhotoBytes.LongLength, int.MaxValue),
                },
            },
            stopwatch.Elapsed);
    }

    private static EnrollmentProgressOperationRequest CreateOperationRequest(
        EnrollmentPipelineContext context,
        EnrollmentStatus expectedStatus) =>
        new()
        {
            ItemId = context.ItemContext.ItemId,
            BatchId = context.ItemContext.BatchId,
            TenantId = context.ItemContext.TenantId,
            ExpectedStatus = expectedStatus,
            CorrelationId = context.ItemContext.CorrelationId,
            ExecutionTraceId = context.ItemContext.ExecutionTraceId,
            PipelineVersion = context.ItemContext.PipelineVersion,
        };

    private static EnrollmentStageProgressRequest CreateStageRequest(
        EnrollmentPipelineContext context,
        EnrollmentStatus expectedStatus,
        EnrollmentPipelineStage stage) =>
        new()
        {
            ItemId = context.ItemContext.ItemId,
            BatchId = context.ItemContext.BatchId,
            TenantId = context.ItemContext.TenantId,
            ExpectedStatus = expectedStatus,
            Stage = stage,
            CorrelationId = context.ItemContext.CorrelationId,
            ExecutionTraceId = context.ItemContext.ExecutionTraceId,
            PipelineVersion = context.ItemContext.PipelineVersion,
        };
}
