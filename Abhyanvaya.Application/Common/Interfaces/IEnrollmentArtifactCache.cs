using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Future cache seam for resolved enrollment artifacts (AI20.PHASE2.1.5A). Not used in hot path yet.</summary>
public interface IEnrollmentArtifactCache
{
    Task<EnrollmentArtifact?> LookupAsync(
        Guid manifestId,
        string artifactType,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        Guid manifestId,
        string artifactType,
        EnrollmentArtifact artifact,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        Guid manifestId,
        string? artifactType = null,
        CancellationToken cancellationToken = default);
}
