using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

internal sealed class DefaultEnrollmentStoragePolicy : IEnrollmentStoragePolicy
{
    public Task<EnrollmentStoragePolicyDecision> ResolveAsync(
        EnrollmentStoragePolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var enabled = new HashSet<string>(StringComparer.Ordinal)
        {
            EnrollmentArtifactTypeNames.AlignedFace,
            EnrollmentArtifactTypeNames.ValidationReport,
        };

        return Task.FromResult(new EnrollmentStoragePolicyDecision
        {
            EnabledArtifactTypes = enabled,
            RetentionDays = 365,
            EnableCompression = false,
            EnableEncryption = false,
            StorageTier = "standard",
        });
    }
}
