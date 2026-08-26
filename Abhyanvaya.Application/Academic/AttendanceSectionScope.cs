using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

public enum AcademicYearAuthorityStatus
{
    ExactlyOne = 0,
    None = 1,
    Multiple = 2,
}

public sealed record AcademicYearAuthorityResult(
    AcademicYearAuthorityStatus Status,
    int? AcademicYearId,
    string? Error,
    IReadOnlyList<int> CurrentYearIds);

/// <summary>
/// AI29.1D Prompt 11A/11B — server-side section filter scope for attendance roster.
/// Fail-closed academic year authority for Section-scoped attendance only.
/// Does not resolve timetable sessions; does not invent eligibility beyond StudentSections + academic scope.
/// </summary>
public static class AttendanceSectionScope
{
    public const string NoCurrentAcademicYearMessage = "Current academic year is not configured.";
    public const string MultipleCurrentAcademicYearsMessage =
        "Multiple current academic years are configured. Section-scoped attendance is unavailable until exactly one current academic year is set.";
    public const string SectionOutOfScopeMessage =
        "One or more sections are outside the authorized Course / Group / Semester / Academic Year scope.";

    /// <summary>
    /// Normalize requested section ids. Empty means legacy full cohort (no section filter).
    /// </summary>
    public static IReadOnlyList<int> NormalizeRequestedIds(int? sectionId, IEnumerable<int>? sectionIds)
    {
        var ids = (sectionIds ?? Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (sectionId is > 0 && !ids.Contains(sectionId.Value))
            ids.Add(sectionId.Value);
        return ids;
    }

    /// <summary>
    /// Deterministic current Academic Year authority: ExactlyOne | None | Multiple (never guess).
    /// </summary>
    public static async Task<AcademicYearAuthorityResult> ResolveAuthoritativeCurrentAcademicYearAsync(
        IApplicationDbContext db,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var currentIds = await db.SchedulingAcademicYears.AsNoTracking()
            .Where(y => y.TenantId == tenantId && y.IsCurrent)
            .Select(y => y.Id)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (currentIds.Count == 0)
        {
            return new AcademicYearAuthorityResult(
                AcademicYearAuthorityStatus.None,
                null,
                NoCurrentAcademicYearMessage,
                currentIds);
        }

        if (currentIds.Count > 1)
        {
            return new AcademicYearAuthorityResult(
                AcademicYearAuthorityStatus.Multiple,
                null,
                MultipleCurrentAcademicYearsMessage,
                currentIds);
        }

        return new AcademicYearAuthorityResult(
            AcademicYearAuthorityStatus.ExactlyOne,
            currentIds[0],
            null,
            currentIds);
    }

    /// <summary>
    /// Validates section ids against Tenant + authoritative current Academic Year + Course + Group + Semester.
    /// Empty requested ids → success with no filter (legacy; academic year not required).
    /// </summary>
    public static async Task<(IReadOnlyList<int> ScopeIds, string? Error)> ValidateSectionIdsAsync(
        IApplicationDbContext db,
        int tenantId,
        int courseId,
        int groupId,
        int semesterId,
        IReadOnlyList<int> requestedSectionIds,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (requestedSectionIds.Count == 0)
            return (Array.Empty<int>(), null);

        var authority = await ResolveAuthoritativeCurrentAcademicYearAsync(db, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (authority.Status == AcademicYearAuthorityStatus.None)
            return (Array.Empty<int>(), authority.Error);

        if (authority.Status == AcademicYearAuthorityStatus.Multiple)
        {
            logger?.LogWarning(
                "Section-scoped attendance rejected: multiple IsCurrent academic years for TenantId={TenantId}. YearIds={YearIds}",
                tenantId,
                string.Join(',', authority.CurrentYearIds));
            return (Array.Empty<int>(), authority.Error);
        }

        var academicYearId = authority.AcademicYearId!.Value;

        var inScopeIds = await db.Sections.AsNoTracking()
            .Where(s =>
                s.TenantId == tenantId
                && s.AcademicYearId == academicYearId
                && requestedSectionIds.Contains(s.Id)
                && s.CourseId == courseId
                && s.GroupId == groupId
                && s.SemesterId == semesterId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (inScopeIds.Count != requestedSectionIds.Count)
            return (Array.Empty<int>(), SectionOutOfScopeMessage);

        return (inScopeIds, null);
    }

    /// <summary>
    /// Restricts student cohort to current StudentSections membership for the validated section ids.
    /// </summary>
    public static IQueryable<Student> ApplyStudentSectionFilter(
        IQueryable<Student> students,
        IApplicationDbContext db,
        int tenantId,
        IReadOnlyList<int> scopeSectionIds)
    {
        if (scopeSectionIds.Count == 0)
            return students;

        var allocatedStudentIds = db.StudentSections.AsNoTracking()
            .Where(ss =>
                ss.TenantId == tenantId
                && ss.IsCurrent
                && scopeSectionIds.Contains(ss.SectionId))
            .Select(ss => ss.StudentId);

        return students.Where(x => allocatedStudentIds.Contains(x.Id));
    }
}
