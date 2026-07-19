using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration;

public sealed class DefaultEnrollmentRetryPolicy : IEnrollmentRetryPolicy
{
    private static readonly HashSet<string> TransientFailureCodes =
    [
        EnrollmentPipelineFailureCodes.UnexpectedFailure,
        "storage.failure",
        EnrollmentEmbeddingFailureCodes.EmbeddingFailure,
        EnrollmentPersistenceFailureCodes.DatabaseFailure,
        EnrollmentPersistenceFailureCodes.ConcurrencyConflict,
    ];

    public EnrollmentRetryDecision Evaluate(
        IEnrollmentPipelineStage stage,
        EnrollmentPipelineStageExecutionResult result,
        int attemptCount)
    {
        if (!stage.SupportsRetry || result.Success || result.IsCancelled)
        {
            return EnrollmentRetryDecision.NoRetry();
        }

        if (result.FailureCategory is FailureCategory.StorageUploadFailed or FailureCategory.EmbeddingEngineFailed)
        {
            return TryRetry(attemptCount, "Transient failure category.");
        }

        if (!string.IsNullOrWhiteSpace(result.FailureCode)
            && TransientFailureCodes.Contains(result.FailureCode))
        {
            return TryRetry(attemptCount, result.FailureReason);
        }

        if (result.IsRetryable)
        {
            return TryRetry(attemptCount, result.FailureReason);
        }

        return EnrollmentRetryDecision.NoRetry(result.FailureReason);
    }

    private static EnrollmentRetryDecision TryRetry(int attemptCount, string? reason)
    {
        const int maxAttempts = 3;
        if (attemptCount >= maxAttempts)
        {
            return EnrollmentRetryDecision.NoRetry("Maximum retry attempts reached.");
        }

        var delayMs = (int)Math.Pow(2, attemptCount) * 250;
        return EnrollmentRetryDecision.Retry(TimeSpan.FromMilliseconds(delayMs), reason);
    }
}
