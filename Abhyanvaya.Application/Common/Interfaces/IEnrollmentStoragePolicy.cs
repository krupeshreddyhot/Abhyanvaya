using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Resolves institution-specific enrollment storage behavior (AI20.PHASE2.1.5).</summary>
public interface IEnrollmentStoragePolicy
{
    Task<EnrollmentStoragePolicyDecision> ResolveAsync(
        EnrollmentStoragePolicyRequest request,
        CancellationToken cancellationToken = default);
}
