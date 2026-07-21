using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Enrollment.Queries;

public sealed class EnrollmentEligibleStudentQuery : IEnrollmentEligibleStudentQuery
{
    private readonly IApplicationDbContext _context;

    public EnrollmentEligibleStudentQuery(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EnrollmentEligibleStudent>> GetEligibleStudentsAsync(
        EnrollmentStudentDiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Students
            .AsNoTracking()
            .Where(s => s.TenantId == criteria.TenantId && !s.IsDeleted);

        if (criteria.CourseId.HasValue)
        {
            query = query.Where(s => s.CourseId == criteria.CourseId.Value);
        }

        if (criteria.GroupId.HasValue)
        {
            query = query.Where(s => s.GroupId == criteria.GroupId.Value);
        }

        if (criteria.Batch.HasValue)
        {
            query = query.Where(s => s.Batch == criteria.Batch.Value);
        }

        if (criteria.SubjectId.HasValue)
        {
            var studentIdsWithSubject = _context.StudentSubjects
                .AsNoTracking()
                .Where(ss => ss.SubjectId == criteria.SubjectId.Value && !ss.IsDeleted)
                .Select(ss => ss.StudentId);

            query = query.Where(s => studentIdsWithSubject.Contains(s.Id));
        }

        if (!string.IsNullOrWhiteSpace(criteria.StudentFilter))
        {
            var filter = criteria.StudentFilter.Trim();
            query = query.Where(s =>
                s.StudentNumber.Contains(filter)
                || s.Name.Contains(filter));
        }

        if (!criteria.ForceReEnrollment)
        {
            var embeddedStudentIds = await _context.StudentFaceEmbeddings
                .AsNoTracking()
                .Where(e =>
                    e.TenantId == criteria.TenantId
                    && e.IsActive
                    && e.EmbeddingStatus == EmbeddingStatus.Completed)
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (embeddedStudentIds.Count > 0)
            {
                query = query.Where(s => !embeddedStudentIds.Contains(s.Id));
            }
        }

        return await query
            .OrderBy(s => s.StudentNumber)
            .Select(s => new EnrollmentEligibleStudent
            {
                StudentId = s.Id,
                StudentNumber = s.StudentNumber,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AdmissionBatchHasStudentsAsync(
        int tenantId,
        int admissionBatch,
        CancellationToken cancellationToken = default) =>
        _context.Students.AnyAsync(
            s => s.TenantId == tenantId && !s.IsDeleted && s.Batch == admissionBatch,
            cancellationToken);
}
