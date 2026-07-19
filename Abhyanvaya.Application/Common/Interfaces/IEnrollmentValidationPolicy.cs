using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Resolves institution-specific validation thresholds and profile selection (AI20.PHASE2.1.4A).
/// </summary>
public interface IEnrollmentValidationPolicy
{
    Task<EnrollmentValidationPolicyDecision> ResolveAsync(
        EnrollmentValidationPolicyRequest request,
        CancellationToken cancellationToken = default);
}
