using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories;

public sealed class StudentEnrollmentBatchRepository : IStudentEnrollmentBatchRepository
{
    private readonly ApplicationDbContext _context;

    public StudentEnrollmentBatchRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateBatchAsync(StudentEnrollmentBatch batch, CancellationToken cancellationToken = default)
    {
        await _context.Set<StudentEnrollmentBatch>().AddAsync(batch, cancellationToken);
    }

    public Task<StudentEnrollmentBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentBatches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

    public Task<StudentEnrollmentBatch?> GetBatchAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentBatches.FirstOrDefaultAsync(
            b => b.Id == batchId && b.TenantId == tenantId,
            cancellationToken);

    public Task UpdateBatchAsync(StudentEnrollmentBatch batch, CancellationToken cancellationToken = default)
    {
        _context.Set<StudentEnrollmentBatch>().Update(batch);
        return Task.CompletedTask;
    }

    public async Task<EnrollmentStatistics?> GetStatisticsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var counters = await _context.StudentEnrollmentBatches
            .AsNoTracking()
            .Where(b => b.Id == batchId)
            .Select(b => new
            {
                b.TotalStudents,
                b.PendingCount,
                b.DownloadingCount,
                b.ValidatingCount,
                b.EmbeddingCount,
                b.CompletedCount,
                b.FailedCount,
                b.RetryRequiredCount,
                b.CancelledCount,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return counters == null
            ? null
            : new EnrollmentStatistics(
                counters.TotalStudents,
                counters.PendingCount,
                counters.DownloadingCount,
                counters.ValidatingCount,
                counters.EmbeddingCount,
                counters.CompletedCount,
                counters.FailedCount,
                counters.RetryRequiredCount,
                counters.CancelledCount);
    }

    public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentBatches.AnyAsync(b => b.Id == batchId, cancellationToken);

    public async Task<IReadOnlyList<StudentEnrollmentBatch>> GetByCollegeAsync(
        int tenantId,
        int collegeId,
        int academicYear,
        CancellationToken cancellationToken = default) =>
        await _context.StudentEnrollmentBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.CollegeId == collegeId && b.AcademicYear == academicYear)
            .OrderByDescending(b => b.CreatedUtc)
            .ToListAsync(cancellationToken);

    public Task<bool> HasActiveBatchAsync(
        int tenantId,
        int collegeId,
        int academicYear,
        Guid? excludeBatchId = null,
        CancellationToken cancellationToken = default) =>
        _context.StudentEnrollmentBatches.AnyAsync(
            b => b.TenantId == tenantId
                 && b.CollegeId == collegeId
                 && b.AcademicYear == academicYear
                 && (b.Status == BatchStatus.Created || b.Status == BatchStatus.Running)
                 && (excludeBatchId == null || b.Id != excludeBatchId),
            cancellationToken);
}
