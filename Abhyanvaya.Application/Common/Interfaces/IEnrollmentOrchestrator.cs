using Abhyanvaya.Application.Enrollment.Orchestration;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Sole coordinator of the enrollment pipeline (AI20.PHASE2.1.8). Owns sequencing, workflow,
/// error propagation, cancellation, and progress invocation — no AI, storage, persistence, or SQL.
/// </summary>
public interface IEnrollmentOrchestrator
{
    Task<EnrollmentPipelineResult> ProcessItemAsync(
        EnrollmentPipelineRequest request,
        CancellationToken cancellationToken = default);
}
