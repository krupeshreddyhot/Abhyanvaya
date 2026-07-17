using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Sole owner of enrollment artifact persistence (AI20.PHASE2.1.5). Stores validated artifacts only;
/// never validates, embeds, or updates batch/progress state.
/// </summary>
public interface IEnrollmentStorageService
{
    Task<EnrollmentStorageResult> StoreAsync(
        EnrollmentStorageRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentStorageResult?> RetrieveAsync(
        Guid storageRecordId,
        CancellationToken cancellationToken = default);

    /// <summary>Future: soft-delete or tombstone artifacts. Not implemented in 2.1.5.</summary>
    Task<EnrollmentStorageResult> DeleteAsync(
        Guid storageRecordId,
        CancellationToken cancellationToken = default);
}
