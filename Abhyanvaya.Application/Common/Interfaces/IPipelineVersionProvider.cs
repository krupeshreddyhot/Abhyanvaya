using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Application.Enrollment.Versioning;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Resolves the active pipeline version for new batches (docs/AI20_PHASE2_PIPELINE_VERSIONING.md).</summary>
public interface IPipelineVersionProvider
{
    PipelineVersion GetActiveVersionForNewBatch(EnrollmentBatchRequest request);

    bool VersionExists(PipelineVersion version);
}
