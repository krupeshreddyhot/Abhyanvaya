using Abhyanvaya.Application.Enrollment.Orchestration;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Executes ordered enrollment pipeline stages with cancellation, retry, metrics, and logging
/// (AI20.PHASE2.1.8). Reusable workflow engine delegated to by the orchestrator.
/// </summary>
public interface IEnrollmentPipelineExecutor
{
    Task<EnrollmentPipelineResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default);
}
