using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.ValueObjects;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Persistence access for <see cref="StudentEnrollmentBatch"/>. Thin data access only — batch lifecycle,
/// counter transitions, and orchestration belong to enrollment services (see docs/AI20_PHASE2_ENGINE_CONTRACTS.md).
/// </summary>
public interface IStudentEnrollmentBatchRepository
{
    Task CreateBatchAsync(StudentEnrollmentBatch batch, CancellationToken cancellationToken = default);

    Task<StudentEnrollmentBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<StudentEnrollmentBatch?> GetBatchAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task UpdateBatchAsync(StudentEnrollmentBatch batch, CancellationToken cancellationToken = default);

    Task<EnrollmentStatistics?> GetStatisticsAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentEnrollmentBatch>> GetByCollegeAsync(
        int tenantId,
        int collegeId,
        int academicYear,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveBatchAsync(
        int tenantId,
        int collegeId,
        int academicYear,
        Guid? excludeBatchId = null,
        CancellationToken cancellationToken = default);
}
