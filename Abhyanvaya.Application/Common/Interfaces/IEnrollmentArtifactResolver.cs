using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Sole component responsible for resolving enrollment artifacts from storage (AI20.PHASE2.1.5A).
/// Downstream services (e.g. embedding) must use this — never object keys or storage providers.
/// </summary>
public interface IEnrollmentArtifactResolver
{
    Task<EnrollmentArtifactResolveResult> ResolveAsync(
        EnrollmentArtifactResolveRequest request,
        CancellationToken cancellationToken = default);
}
