using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Base abstraction for one enrollment pipeline stage (AI20.PHASE2.1.8). Stages are discovered
/// dynamically via <see cref="IEnrollmentPipelineRegistry"/> — never hardcoded in the orchestrator.
/// </summary>
public interface IEnrollmentPipelineStage
{
    /// <summary>Manifest stage identity when mapped to a manifest entry; null for auxiliary stages.</summary>
    EnrollmentPipelineStage? ManifestStage { get; }

    string Name { get; }

    int Order { get; }

    string Description { get; }

    string Version { get; }

    bool SupportsRetry { get; }

    bool SupportsResume { get; }

    Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
        EnrollmentPipelineContext context,
        CancellationToken cancellationToken = default);
}
