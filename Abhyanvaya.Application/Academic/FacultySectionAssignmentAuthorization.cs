using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1D.15A Prompt 7 — server-side Faculty→Section assign authorization.
/// Reuses existing Section / Staff / academic-year scope; no parallel auth model.
/// Never substitutes another faculty for a rejected id.
/// </summary>
public static class FacultySectionAssignmentAuthorization
{
    public const string UnauthorizedFacultyMessage =
        "Unauthorized faculty. The selected staff member is not available for allocation in this tenant.";

    public const string InactiveFacultyMessage =
        "Inactive or invalid faculty. The selected staff member cannot be allocated.";

    public const string SectionOutOfAcademicScopeMessage =
        "Section is outside the authorized Academic Year / Course / Group / Semester scope.";

    public const string InvalidAcademicYearMessage = "Invalid academic year.";
    public const string InvalidCourseMessage = "Invalid course.";
    public const string InvalidGroupMessage = "Invalid group for course.";
    public const string InvalidSemesterMessage = "Invalid semester.";
    public const string InvalidRequestMessage = "Faculty, section and academic year are required.";

    public sealed record AssignValidationResult(
        bool Ok,
        Section? Section,
        int FacultyId,
        string? Error,
        bool SectionNotFound);

    /// <summary>
    /// Validates Tenant + Faculty + Academic Year + Course + Group + Semester + Section.
    /// On failure, FacultyId remains the requested id (never silently substituted).
    /// </summary>
    public static async Task<AssignValidationResult> ValidateAssignAsync(
        IApplicationDbContext db,
        int tenantId,
        AssignFacultySectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestedFacultyId = request.FacultyId;
        if (request.FacultyId <= 0 || request.SectionId <= 0 || request.AcademicYearId <= 0)
        {
            return new AssignValidationResult(false, null, requestedFacultyId, InvalidRequestMessage, false);
        }

        var section = await db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == request.SectionId && s.TenantId == tenantId && !s.IsDeleted,
                cancellationToken)
            .ConfigureAwait(false);

        if (section is null)
        {
            return new AssignValidationResult(false, null, requestedFacultyId, "Section not found.", true);
        }

        // Request academic year must match the section's authoritative academic year (no client drift).
        if (section.AcademicYearId != request.AcademicYearId)
        {
            return new AssignValidationResult(
                false, null, requestedFacultyId, SectionOutOfAcademicScopeMessage, false);
        }

        var scopeError = await ValidateAcademicScopeAsync(
                db,
                tenantId,
                section.AcademicYearId,
                section.CourseId,
                section.GroupId,
                section.SemesterId,
                cancellationToken)
            .ConfigureAwait(false);
        if (scopeError != null)
        {
            return new AssignValidationResult(false, null, requestedFacultyId, scopeError, false);
        }

        // Re-assert section membership in that exact academic scope (defense in depth).
        var sectionInScope = await db.Sections.AsNoTracking()
            .AnyAsync(
                s =>
                    s.Id == section.Id
                    && s.TenantId == tenantId
                    && !s.IsDeleted
                    && s.AcademicYearId == section.AcademicYearId
                    && s.CourseId == section.CourseId
                    && s.GroupId == section.GroupId
                    && s.SemesterId == section.SemesterId,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sectionInScope)
        {
            return new AssignValidationResult(
                false, null, requestedFacultyId, SectionOutOfAcademicScopeMessage, false);
        }

        var facultyError = await ValidateFacultyAsync(db, tenantId, requestedFacultyId, cancellationToken)
            .ConfigureAwait(false);
        if (facultyError != null)
        {
            return new AssignValidationResult(false, null, requestedFacultyId, facultyError, false);
        }

        return new AssignValidationResult(true, section, requestedFacultyId, null, false);
    }

    public static async Task<string?> ValidateAcademicScopeAsync(
        IApplicationDbContext db,
        int tenantId,
        int academicYearId,
        int courseId,
        int groupId,
        int semesterId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.SchedulingAcademicYears.AsNoTracking()
                .AnyAsync(y => y.Id == academicYearId && y.TenantId == tenantId && !y.IsDeleted, cancellationToken)
                .ConfigureAwait(false))
            return InvalidAcademicYearMessage;

        if (!await db.Courses.AsNoTracking()
                .AnyAsync(c => c.Id == courseId && c.TenantId == tenantId && !c.IsDeleted, cancellationToken)
                .ConfigureAwait(false))
            return InvalidCourseMessage;

        if (!await db.Groups.AsNoTracking()
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == tenantId && !g.IsDeleted && g.CourseId == courseId,
                    cancellationToken)
                .ConfigureAwait(false))
            return InvalidGroupMessage;

        if (!await db.Semesters.AsNoTracking()
                .AnyAsync(s => s.Id == semesterId && s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
                .ConfigureAwait(false))
            return InvalidSemesterMessage;

        return null;
    }

    public static async Task<string?> ValidateFacultyAsync(
        IApplicationDbContext db,
        int tenantId,
        int facultyId,
        CancellationToken cancellationToken = default)
    {
        // Outside tenant / missing / soft-deleted → unauthorized (do not reveal cross-tenant existence).
        var staff = await db.StaffMembers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == facultyId, cancellationToken)
            .ConfigureAwait(false);

        if (staff is null || staff.TenantId != tenantId || staff.IsDeleted)
            return UnauthorizedFacultyMessage;

        if (staff.EmploymentStatusId is int statusId)
        {
            var status = await db.EmploymentStatusLookups.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == statusId, cancellationToken)
                .ConfigureAwait(false);
            if (status is null || status.IsDeleted || !status.IsActive)
                return InactiveFacultyMessage;

            var code = status.Code?.Trim().ToUpperInvariant();
            if (code is "INACTIVE" or "TERMINATED" or "RESIGNED" or "RETIRED")
                return InactiveFacultyMessage;
        }

        var staffType = await db.StaffTypeLookups.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == staff.StaffTypeId, cancellationToken)
            .ConfigureAwait(false);
        if (staffType is null || staffType.IsDeleted || !staffType.IsActive)
            return InactiveFacultyMessage;

        return null;
    }
}
