using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Validation result cache seam — framework only; no persistence in this phase (AI20.PHASE2.1.4A).
/// </summary>
public interface IValidationCache
{
    Task<EnrollmentValidationArtifact?> LookupAsync(
        string cacheKey,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        string cacheKey,
        EnrollmentValidationArtifact artifact,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        string cacheKey,
        CancellationToken cancellationToken = default);
}
