using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;

public sealed class ValidationEnrollmentPipelineStage : IEnrollmentPipelineStage
{
    private readonly IEnrollmentValidationService _validationService;
    private readonly IEnrollmentProgressReporter _progressReporter;

    public ValidationEnrollmentPipelineStage(
        IEnrollmentValidationService validationService,
        IEnrollmentProgressReporter progressReporter)
    {
        _validationService = validationService;
        _progressReporter = progressReporter;
    }

    public EnrollmentPipelineStage? ManifestStage => EnrollmentPipelineStage.Validation;
    public string Name => "Validation";
    public int Order => 100;
    public string Description => "Evaluate enrollment photo quality and produce a validation artifact.";
    public string Version => "1.0";
    public bool SupportsRetry => false;
    public bool SupportsResume => false;

    public async Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var itemContext = context.ItemContext;

        if (context.Request.ItemStatus == EnrollmentStatus.Downloaded)
        {
            var storageProgress = await _progressReporter.MarkStageCompletedAsync(
                CreateStageRequest(context, EnrollmentStatus.Downloaded, EnrollmentPipelineStage.Storage),
                cancellationToken);

            if (!storageProgress.Applied)
            {
                stopwatch.Stop();
                return EnrollmentPipelineStageExecutionResult.Failed(
                    context with { State = EnrollmentPipelineState.Failed },
                    stopwatch.Elapsed,
                    EnrollmentPipelineFailureCodes.ProgressConflict,
                    storageProgress.Reason ?? "Unable to transition to validating.",
                    isRetryable: storageProgress.ConcurrencyConflict);
            }

            context = context with { Request = context.Request with { ItemStatus = EnrollmentStatus.Validating } };
        }

        if (context.PhotoBytes is not { Length: > 0 })
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.StageFailed,
                "Validation requires downloaded photo bytes.",
                FailureCategory.InvalidImage);
        }

        await using var imageStream = new MemoryStream(context.PhotoBytes, writable: false);
        var validationResult = await _validationService.ValidateAsync(
            new EnrollmentValidationRequest
            {
                StudentId = itemContext.StudentId,
                BatchId = itemContext.BatchId,
                ExecutionContext = new EnrollmentValidationExecutionContext
                {
                    TenantId = itemContext.TenantId,
                    CorrelationId = itemContext.CorrelationId,
                    ExecutionTraceId = itemContext.ExecutionTraceId,
                    PipelineVersion = itemContext.PipelineVersion,
                },
                ImageStream = imageStream,
                ImageMetadata = new EnrollmentImageMetadata
                {
                    FileName = $"{itemContext.StudentNumber}.jpg",
                    ContentType = context.ContentType,
                    ByteSize = context.PhotoByteSize ?? context.PhotoBytes.LongLength,
                },
            },
            cancellationToken);

        if (!validationResult.ValidationPassed || validationResult.Artifact is null)
        {
            stopwatch.Stop();
            await _progressReporter.MarkStageFailedAsync(
                CreateStageRequest(context, EnrollmentStatus.Validating, EnrollmentPipelineStage.Validation),
                EnrollmentStatus.Failed,
                cancellationToken);

            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed, ValidationResult = validationResult },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.StageFailed,
                validationResult.FailureReason ?? "Validation failed.",
                validationResult.FailureCategory ?? FailureCategory.Unknown);
        }

        var qualityWarnings = validationResult.Report.Warnings.Count > 0
            ? validationResult.Report.Warnings.ToList()
            : new List<string>();

        if (!validationResult.Report.EmbeddingEligible)
        {
            qualityWarnings.Add("Face embedding skipped: photo stored on student profile only.");
        }

        var validationProgress = await _progressReporter.MarkStageCompletedAsync(
            CreateStageRequest(context, EnrollmentStatus.Validating, EnrollmentPipelineStage.Validation),
            cancellationToken);

        if (!validationProgress.Applied)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with
                {
                    State = EnrollmentPipelineState.Failed,
                    ValidationResult = validationResult,
                    ValidationArtifact = validationResult.Artifact,
                },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.ProgressConflict,
                validationProgress.Reason ?? "Unable to mark validation complete.",
                isRetryable: validationProgress.ConcurrencyConflict);
        }

        stopwatch.Stop();
        return EnrollmentPipelineStageExecutionResult.Succeeded(
            context with
            {
                State = EnrollmentPipelineState.Validated,
                CurrentStage = EnrollmentPipelineStage.Validation,
                ValidationResult = validationResult,
                ValidationArtifact = validationResult.Artifact,
                EmbeddingEligible = validationResult.Report.EmbeddingEligible,
                Request = context.Request with { ItemStatus = EnrollmentStatus.Embedding },
                Warnings = qualityWarnings.Count > 0 ? qualityWarnings : context.Warnings,
            },
            stopwatch.Elapsed);
    }

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
