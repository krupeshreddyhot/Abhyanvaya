using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

public sealed class EnrollmentReferenceValidator : IEnrollmentReferenceValidator
{
    private readonly IApplicationDbContext _context;
    private readonly IEnrollmentEligibleStudentQuery _eligibleStudentQuery;

    public EnrollmentReferenceValidator(
        IApplicationDbContext context,
        IEnrollmentEligibleStudentQuery eligibleStudentQuery)
    {
        _context = context;
        _eligibleStudentQuery = eligibleStudentQuery;
    }

    public async Task<EnrollmentReferenceValidationResult> ValidateAsync(
        EnrollmentBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TenantId <= 0
            || request.UniversityId <= 0
            || request.CollegeId <= 0
            || request.AcademicYear <= 0
            || request.RequestedByUserId <= 0)
        {
            return EnrollmentReferenceValidationResult.Fail(
                EnrollmentBatchFailureCode.InvalidRequest,
                "TenantId, UniversityId, CollegeId, AcademicYear, and RequestedByUserId are required.");
        }

        var college = await _context.Colleges
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == request.CollegeId
                     && c.TenantId == request.TenantId
                     && !c.IsDeleted,
                cancellationToken);

        if (college == null)
        {
            return EnrollmentReferenceValidationResult.Fail(
                EnrollmentBatchFailureCode.CollegeNotFound,
                $"College {request.CollegeId} was not found for tenant {request.TenantId}.");
        }

        if (college.UniversityId != request.UniversityId)
        {
            return EnrollmentReferenceValidationResult.Fail(
                EnrollmentBatchFailureCode.CollegeNotFound,
                $"College {request.CollegeId} does not belong to university {request.UniversityId}.");
        }

        if (request.CourseId.HasValue)
        {
            var courseExists = await _context.Courses.AnyAsync(
                c => c.Id == request.CourseId.Value && c.TenantId == request.TenantId && !c.IsDeleted,
                cancellationToken);

            if (!courseExists)
            {
                return EnrollmentReferenceValidationResult.Fail(
                    EnrollmentBatchFailureCode.CourseNotFound,
                    $"Course {request.CourseId.Value} was not found.");
            }
        }

        if (request.GroupId.HasValue)
        {
            var groupExists = await _context.Groups.AnyAsync(
                g => g.Id == request.GroupId.Value && g.TenantId == request.TenantId && !g.IsDeleted,
                cancellationToken);

            if (!groupExists)
            {
                return EnrollmentReferenceValidationResult.Fail(
                    EnrollmentBatchFailureCode.GroupNotFound,
                    $"Group {request.GroupId.Value} was not found.");
            }
        }

        if (request.Batch.HasValue)
        {
            var batchExists = await _eligibleStudentQuery.AdmissionBatchHasStudentsAsync(
                request.TenantId,
                request.Batch.Value,
                cancellationToken);

            if (!batchExists)
            {
                return EnrollmentReferenceValidationResult.Fail(
                    EnrollmentBatchFailureCode.BatchNotFound,
                    $"Admission batch {request.Batch.Value} has no active students.");
            }
        }

        if (request.SubjectId.HasValue)
        {
            var subjectExists = await _context.Subjects.AnyAsync(
                s => s.Id == request.SubjectId.Value && s.TenantId == request.TenantId && !s.IsDeleted,
                cancellationToken);

            if (!subjectExists)
            {
                return EnrollmentReferenceValidationResult.Fail(
                    EnrollmentBatchFailureCode.SubjectNotFound,
                    $"Subject {request.SubjectId.Value} was not found.");
            }
        }

        return EnrollmentReferenceValidationResult.Ok(college.Code);
    }
}
