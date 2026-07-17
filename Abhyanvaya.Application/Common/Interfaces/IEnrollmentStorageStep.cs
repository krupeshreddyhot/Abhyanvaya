using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Pluggable enrollment storage pipeline step (AI20.PHASE2.1.5B / 5G metadata).</summary>
public interface IEnrollmentStorageStep
{
    string Name { get; }

    int Order { get; }

    string Category { get; }

    bool SupportsRollback { get; }

    bool IsOptional { get; }

    string? FeatureFlag { get; }

    string Description { get; }

    string Version { get; }

    bool Enabled { get; }

    Task ExecuteAsync(EnrollmentStoragePipelineContext context, CancellationToken cancellationToken = default);
}
