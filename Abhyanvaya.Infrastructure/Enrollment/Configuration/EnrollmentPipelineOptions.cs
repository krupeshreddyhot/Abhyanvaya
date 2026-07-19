using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;

namespace Abhyanvaya.Infrastructure.Enrollment.Configuration;

public sealed class EnrollmentPipelineOptions
{
    public const string SectionName = "EnrollmentPipeline";

    public string PipelineName { get; set; } = "StudentEnrollment";

    public int ActiveVersion { get; set; } = 1;

    public Dictionary<int, EnrollmentPipelineVersionOptions> Versions { get; set; } = new();
}

public sealed class EnrollmentPipelineVersionOptions
{
    public int SchemaVersion { get; set; } = 1;
    public string? Description { get; set; }
}

public static class EnrollmentPipelineDefaults
{
    public const string PipelineName = "StudentEnrollment";

    public static PipelineManifest CreateV1Manifest() =>
        new()
        {
            PipelineName = PipelineName,
            PipelineVersion = 1,
            SchemaVersion = 1,
            Description = "Baseline V1 — five fixed core stages, no optional AI stages enabled.",
            PipelineValidationRules = new PipelineValidationRules
            {
                RequireAllCoreStages = true,
                EnforceDependencyOrder = true,
            },
            Stages =
            [
                new StageManifestEntry
                {
                    Stage = EnrollmentPipelineStage.Download,
                    Enabled = true,
                    Kind = StageKind.Core,
                    Order = 0,
                    Required = true,
                    Optional = false,
                },
                new StageManifestEntry
                {
                    Stage = EnrollmentPipelineStage.Validation,
                    Enabled = true,
                    Kind = StageKind.Core,
                    Order = 1,
                    Required = true,
                    Optional = false,
                },
                new StageManifestEntry
                {
                    Stage = EnrollmentPipelineStage.Storage,
                    Enabled = true,
                    Kind = StageKind.Core,
                    Order = 2,
                    Required = true,
                    Optional = false,
                },
                new StageManifestEntry
                {
                    Stage = EnrollmentPipelineStage.Embedding,
                    Enabled = true,
                    Kind = StageKind.Core,
                    Order = 3,
                    Required = true,
                    Optional = false,
                },
                new StageManifestEntry
                {
                    Stage = EnrollmentPipelineStage.Finalize,
                    Enabled = true,
                    Kind = StageKind.Core,
                    Order = 4,
                    Required = true,
                    Optional = false,
                },
            ],
        };
}
