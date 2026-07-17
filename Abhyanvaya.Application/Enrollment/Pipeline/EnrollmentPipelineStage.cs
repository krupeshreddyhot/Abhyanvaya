namespace Abhyanvaya.Application.Enrollment.Pipeline;

/// <summary>Canonical stage identity for pipeline manifests (docs/AI20_PHASE2_PIPELINE_STAGE_ENUM.md).</summary>
public enum EnrollmentPipelineStage
{
    Download = 0,
    Validation = 1,
    Storage = 2,
    Embedding = 3,
    Finalize = 4,
}
