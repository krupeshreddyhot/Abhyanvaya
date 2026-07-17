using Abhyanvaya.Application.Enrollment.Orchestration;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Centralized retry rules for enrollment pipeline stages (AI20.PHASE2.1.8).
/// </summary>
public interface IEnrollmentRetryPolicy
{
    EnrollmentRetryDecision Evaluate(
        IEnrollmentPipelineStage stage,
        EnrollmentPipelineStageExecutionResult result,
        int attemptCount);
}
