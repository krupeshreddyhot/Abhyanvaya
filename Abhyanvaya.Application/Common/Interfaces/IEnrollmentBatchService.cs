using Abhyanvaya.Application.Enrollment;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Command surface for enrollment batch lifecycle (docs/AI20_PHASE2_ENGINE_CONTRACTS.md §3).
/// Phase 2.1.2 implements batch creation only.
/// </summary>
public interface IEnrollmentBatchService
{
    Task<EnrollmentBatchCreateResult> CreateBatchAsync(
        EnrollmentBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentCommandResult> CancelBatchAsync(
        Guid batchId,
        int tenantId,
        int requestedByUserId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentCommandResult> ResumeBatchAsync(
        Guid batchId,
        int tenantId,
        int requestedByUserId,
        CancellationToken cancellationToken = default);
}
