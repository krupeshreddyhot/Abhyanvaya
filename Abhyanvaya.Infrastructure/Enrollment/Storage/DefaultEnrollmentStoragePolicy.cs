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
            RetentionAction = StorageRetentionAction.Retain,
            LifecycleTier = StorageLifecycleTier.Hot,
            EnableCompression = false,
            EnableEncryption = false,
            EnableReplication = false,
            LegalHold = false,
            StorageTier = "standard",
        });
    }
}
