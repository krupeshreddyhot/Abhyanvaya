using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Executes the enrollment storage pipeline. Abstraction supports future decorators
/// (metrics, tracing, retry, circuit breaker, caching) without changing storage service consumers.
/// </summary>
public interface IEnrollmentStoragePipelineExecutor
{
    /// <summary>Runs ordered storage steps against the supplied pipeline context.</summary>
    Task<EnrollmentStoragePipelineContext> ExecuteAsync(
        EnrollmentStoragePipelineContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Returns ordered step metadata for UI, diagnostics, and documentation discovery.</summary>
    IReadOnlyList<StorageStepMetadata> DescribePipeline();
}
