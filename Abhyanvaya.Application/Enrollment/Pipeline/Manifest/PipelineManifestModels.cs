using Abhyanvaya.Application.Enrollment.Pipeline;

namespace Abhyanvaya.Application.Enrollment.Pipeline.Manifest;

public enum StageKind
{
    Core = 0,
    Optional = 1,
}

public sealed record PipelineManifest
{
    public required string PipelineName { get; init; }
    public required int PipelineVersion { get; init; }
    public required int SchemaVersion { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<StageManifestEntry> Stages { get; init; }
    public required PipelineValidationRules PipelineValidationRules { get; init; }
}

public sealed record StageManifestEntry
{
    public required EnrollmentPipelineStage Stage { get; init; }
    public required bool Enabled { get; init; }
    public required StageKind Kind { get; init; }
    public required int Order { get; init; }
    public required bool Required { get; init; }
    public required bool Optional { get; init; }
}

public sealed record PipelineValidationRules
{
    public bool RequireAllCoreStages { get; init; } = true;
    public bool EnforceDependencyOrder { get; init; } = true;
}
