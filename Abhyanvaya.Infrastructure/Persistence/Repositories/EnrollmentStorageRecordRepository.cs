using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories;

internal sealed class EnrollmentStorageRecordRepository : IEnrollmentStorageRecordRepository
{
    private readonly ApplicationDbContext _db;

    public EnrollmentStorageRecordRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<EnrollmentStorageRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Set<EnrollmentStorageRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<EnrollmentStorageRecord?> FindByChecksumAsync(
        int tenantId,
        int studentId,
        string artifactType,
        string checksum,
        CancellationToken cancellationToken = default) =>
        _db.Set<EnrollmentStorageRecord>()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                        && r.StudentId == studentId
                        && r.ArtifactType == artifactType
                        && r.Checksum == checksum)
            .OrderByDescending(r => r.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> GetNextArtifactVersionAsync(
        int tenantId,
        int studentId,
        string artifactType,
        CancellationToken cancellationToken = default)
    {
        var currentMax = await _db.Set<EnrollmentStorageRecord>()
            .Where(r => r.TenantId == tenantId
                        && r.StudentId == studentId
                        && r.ArtifactType == artifactType)
            .Select(r => (int?)r.ArtifactVersion)
            .MaxAsync(cancellationToken);

        return (currentMax ?? 0) + 1;
    }

    public async Task<IReadOnlyList<EnrollmentStorageRecord>> GetByStorageGroupIdAsync(
        Guid storageGroupId,
        CancellationToken cancellationToken = default) =>
        await _db.Set<EnrollmentStorageRecord>()
            .AsNoTracking()
            .Where(r => r.StorageGroupId == storageGroupId)
            .OrderBy(r => r.ArtifactType)
            .ToListAsync(cancellationToken);

    public Task AddAsync(EnrollmentStorageRecord record, CancellationToken cancellationToken = default) =>
        _db.Set<EnrollmentStorageRecord>().AddAsync(record, cancellationToken).AsTask();

    public Task AddRangeAsync(IEnumerable<EnrollmentStorageRecord> records, CancellationToken cancellationToken = default) =>
        _db.Set<EnrollmentStorageRecord>().AddRangeAsync(records, cancellationToken);
}
