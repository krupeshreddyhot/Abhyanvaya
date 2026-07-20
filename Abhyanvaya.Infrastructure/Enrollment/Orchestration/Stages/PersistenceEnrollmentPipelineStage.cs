using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Application.Enrollment.Pipeline;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;

public sealed class PersistenceEnrollmentPipelineStage : IEnrollmentPipelineStage
{
    private readonly IEnrollmentResultWriter _resultWriter;

    public PersistenceEnrollmentPipelineStage(IEnrollmentResultWriter resultWriter)
    {
        _resultWriter = resultWriter;
    }

    public EnrollmentPipelineStage? ManifestStage => null;
    public string Name => "Persistence";
    public int Order => 350;
    public string Description => "Persist the enrollment embedding and finalize enrollment state.";
    public string Version => "1.0";
    public bool SupportsRetry => true;
    public bool SupportsResume => true;

    public async Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (context.EmbeddingArtifact is null)
        {
            stopwatch.Stop();
            if (!context.EmbeddingEligible)
            {
                return EnrollmentPipelineStageExecutionResult.Succeeded(
                    context with { State = EnrollmentPipelineState.Persisted },
                    stopwatch.Elapsed);
            }

            return EnrollmentPipelineStageExecutionResult.Failed(
                context with { State = EnrollmentPipelineState.Failed },
                stopwatch.Elapsed,
                EnrollmentPipelineFailureCodes.StageFailed,
                "Persistence requires an embedding artifact.");
        }

        var persistenceResult = await _resultWriter.PersistEmbeddingAsync(
            new EnrollmentPersistenceRequest
            {
                Artifact = context.EmbeddingArtifact,
                Metadata = context.EmbeddingResult?.Metadata,
                Warnings = context.Warnings.Count > 0 ? context.Warnings : null,
            },
            cancellationToken);

        if (!persistenceResult.Success)
        {
            stopwatch.Stop();
            var isRetryable = persistenceResult.FailureCode is
                EnrollmentPersistenceFailureCodes.DatabaseFailure
                or EnrollmentPersistenceFailureCodes.ConcurrencyConflict;

            return EnrollmentPipelineStageExecutionResult.Failed(
                context with
                {
                    State = EnrollmentPipelineState.Failed,
                    PersistenceResult = persistenceResult,
                },
                stopwatch.Elapsed,
                persistenceResult.FailureCode ?? EnrollmentPipelineFailureCodes.StageFailed,
                persistenceResult.FailureReason ?? "Persistence failed.",
                isRetryable: isRetryable);
        }

        stopwatch.Stop();
        return EnrollmentPipelineStageExecutionResult.Succeeded(
            context with
            {
                State = EnrollmentPipelineState.Persisted,
                PersistenceResult = persistenceResult,
                Request = context.Request with
                {
                    ItemStatus = persistenceResult.Status ?? context.Request.ItemStatus,
                },
            },
            stopwatch.Elapsed);
    }
}
