using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentStorageRecordRepository
{
    Task<EnrollmentStorageRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EnrollmentStorageRecord?> FindByChecksumAsync(
        int tenantId,
        int studentId,
        string artifactType,
        string checksum,
        CancellationToken cancellationToken = default);

    Task<int> GetNextArtifactVersionAsync(
        int tenantId,
        int studentId,
        string artifactType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentStorageRecord>> GetByStorageGroupIdAsync(
        Guid storageGroupId,
        CancellationToken cancellationToken = default);

    Task AddAsync(EnrollmentStorageRecord record, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<EnrollmentStorageRecord> records, CancellationToken cancellationToken = default);
}
