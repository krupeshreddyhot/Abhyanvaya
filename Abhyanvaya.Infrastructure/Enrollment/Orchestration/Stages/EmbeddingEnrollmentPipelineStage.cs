using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;

public sealed class EmbeddingEnrollmentPipelineStage : IEnrollmentPipelineStage
{
    private readonly IEnrollmentEmbeddingService _embeddingService;

    public EmbeddingEnrollmentPipelineStage(IEnrollmentEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public EnrollmentPipelineStage? ManifestStage => EnrollmentPipelineStage.Embedding;
    public string Name => "Embedding";
    public int Order => 300;
    public string Description => "Generate a normalized enrollment face embedding from the storage manifest.";
    public string Version => "1.0";
    public bool SupportsRetry => true;
    public bool SupportsResume => false;

    public async Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var itemContext = context.ItemContext;

        if (!context.EmbeddingEligible)
        {
            stopwatch.Stop();
            var skipWarnings = MergeWarnings(
                context.Warnings,
                ["Face embedding skipped because the photo is not suitable for recognition."]);
            return EnrollmentPipelineStageExecutionResult.Succeeded(
                context with
                {
                    State = EnrollmentPipelineState.Pending,
                    CurrentStage = EnrollmentPipelineStage.Embedding,
                    Warnings = skipWarnings,
                },
                stopwatch.Elapsed);
        }

        if (context.StorageManifest is null)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.StageFailed,
                "Embedding requires a storage manifest.");
        }

        var embeddingResult = await _embeddingService.GenerateAsync(
            new EnrollmentEmbeddingRequest
            {
                Manifest = context.StorageManifest,
                StudentId = itemContext.StudentId,
                BatchId = itemContext.BatchId,
                CorrelationId = itemContext.CorrelationId,
                PipelineVersion = itemContext.PipelineVersion,
            },
            cancellationToken);

        if (!embeddingResult.Success || embeddingResult.Artifact is null)
        {
            stopwatch.Stop();
            return EnrollmentPipelineStageExecutionResult.Failed(
                context with
                {
                    State = EnrollmentPipelineState.Failed,
                    EmbeddingResult = embeddingResult,
                },
                stopwatch.Elapsed,
                embeddingResult.FailureCode ?? EnrollmentEmbeddingFailureCodes.EmbeddingFailure,
                embeddingResult.FailureReason ?? "Embedding generation failed.",
                Domain.Enums.FailureCategory.EmbeddingEngineFailed,
                isRetryable: true);
        }

        var warnings = MergeWarnings(context.Warnings, embeddingResult.Warnings);
        stopwatch.Stop();
        return EnrollmentPipelineStageExecutionResult.Succeeded(
            context with
            {
                State = EnrollmentPipelineState.Embedded,
                CurrentStage = EnrollmentPipelineStage.Embedding,
                EmbeddingResult = embeddingResult,
                EmbeddingArtifact = embeddingResult.Artifact,
                Warnings = warnings,
            },
            stopwatch.Elapsed);
    }

    private static IReadOnlyList<string> MergeWarnings(
        IReadOnlyList<string> existing,
        IReadOnlyList<string>? additional)
    {
        if (additional is null || additional.Count == 0)
        {
            return existing;
        }

        return existing.Concat(additional).Distinct(StringComparer.Ordinal).ToList();
    }
}
