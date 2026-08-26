using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1D.15A Prompt 2–4 — mark/edit section-scope contract + write-scope student integrity.
/// Reuses <see cref="AttendanceSectionScope"/>; does not resolve timetable sessions.
/// </summary>
public static class AttendanceSaveScope
{
    public const string UnauthorizedStudentsMessage =
        "Attendance rejected: one or more students are outside the authorized section scope. No attendance was saved.";

    public const string IncompleteAtomicWriteMessage =
        "Attendance rejected: the write set is incomplete for the authorized section scope. No attendance was saved.";

    /// <summary>
    /// Normalize optional mark/edit section fields.
    /// Empty / omitted → no section scope (legacy). Duplicates and non-positive ids are dropped.
    /// </summary>
    public static IReadOnlyList<int> NormalizeRequestedIds(int? sectionId, IEnumerable<int>? sectionIds) =>
        AttendanceSectionScope.NormalizeRequestedIds(sectionId, sectionIds);

    public static IReadOnlyList<int> Normalize(MarkAttendanceRequest? request) =>
        NormalizeRequestedIds(request?.SectionId, request?.SectionIds);

    public static IReadOnlyList<int> Normalize(EditAttendanceRequest? request) =>
        NormalizeRequestedIds(request?.SectionId, request?.SectionIds);

    public static bool HasSectionScope(IReadOnlyList<int> normalizedSectionIds) =>
        normalizedSectionIds is { Count: > 0 };

    public static bool IsSingleSection(IReadOnlyList<int> normalizedSectionIds) =>
        normalizedSectionIds.Count == 1;

    public static bool IsCombinedSection(IReadOnlyList<int> normalizedSectionIds) =>
        normalizedSectionIds.Count > 1;

    /// <summary>
    /// Prompt 3 — when section ids are supplied: require exactly one current AY and validate each
    /// section against Tenant + Academic Year + Course + Group + Semester (from the subject).
    /// Empty/omitted section ids → legacy (no AY required).
    /// </summary>
    public static async Task<(IReadOnlyList<int> ScopeIds, string? Error)> ValidateWriteSectionScopeAsync(
        IApplicationDbContext db,
        int tenantId,
        int courseId,
        int groupId,
        int semesterId,
        int? sectionId,
        IEnumerable<int>? sectionIds,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var requested = NormalizeRequestedIds(sectionId, sectionIds);
        return await AttendanceSectionScope.ValidateSectionIdsAsync(
                db,
                tenantId,
                courseId,
                groupId,
                semesterId,
                requested,
                logger,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Restrict students to validated section membership. No-op when scope is empty (legacy).
    /// </summary>
    public static IQueryable<Student> ApplyAuthorizedSectionFilter(
        IQueryable<Student> students,
        IApplicationDbContext db,
        int tenantId,
        IReadOnlyList<int> scopeSectionIds) =>
        AttendanceSectionScope.ApplyStudentSectionFilter(students, db, tenantId, scopeSectionIds);

    /// <summary>
    /// Fail-closed: every submitted student number must appear in the authorized set.
    /// </summary>
    public static string? EnsureAllSubmittedStudentsAuthorized(
        IEnumerable<string?> submittedStudentNumbers,
        IEnumerable<string?> authorizedStudentNumbers)
    {
        var submitted = NormalizeStudentNumbers(submittedStudentNumbers);
        if (submitted.Count == 0)
            return "Students list is required";

        var authorized = authorizedStudentNumbers
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .ToHashSet(StringComparer.Ordinal);

        return submitted.Any(n => !authorized.Contains(n))
            ? UnauthorizedStudentsMessage
            : null;
    }

    /// <summary>
    /// Prompt 4 — validate EVERY submitted student for section-scoped writes.
    /// Chain: Submitted Student → current StudentSection → section in authoritative AY scope → Authorized.
    /// sectionIds=[A] ⇒ membership in A; sectionIds=[A,B] ⇒ membership in A OR B.
    /// Browser student lists are never trusted. Empty scope ⇒ no-op (legacy path).
    /// </summary>
    public static async Task<(IReadOnlyList<Student> AuthorizedStudents, string? Error)> ValidateEverySubmittedStudentInSectionScopeAsync(
        IApplicationDbContext db,
        int tenantId,
        int courseId,
        int groupId,
        int semesterId,
        IReadOnlyList<int> validatedScopeSectionIds,
        IEnumerable<string?> submittedStudentNumbers,
        bool requireCourseGroupSemesterMatch = true,
        CancellationToken cancellationToken = default)
    {
        if (validatedScopeSectionIds.Count == 0)
            return (Array.Empty<Student>(), null);

        var submitted = NormalizeStudentNumbers(submittedStudentNumbers);
        if (submitted.Count == 0)
            return (Array.Empty<Student>(), "Students list is required");

        // Re-assert section academic scope (Tenant + current AY + C/G/S) — never trust caller ids alone.
        var (scopeIds, scopeError) = await AttendanceSectionScope.ValidateSectionIdsAsync(
                db,
                tenantId,
                courseId,
                groupId,
                semesterId,
                validatedScopeSectionIds,
                logger: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (scopeError != null)
            return (Array.Empty<Student>(), scopeError);

        var studentsQuery = db.Students.AsNoTracking()
            .Where(s => s.TenantId == tenantId && submitted.Contains(s.StudentNumber));

        if (requireCourseGroupSemesterMatch)
        {
            studentsQuery = studentsQuery.Where(s =>
                s.CourseId == courseId &&
                s.GroupId == groupId &&
                s.SemesterId == semesterId);
        }

        // Current StudentSection ∈ selected section(s) within the validated academic scope.
        var authorizedQuery =
            from s in studentsQuery
            where db.StudentSections.AsNoTracking().Any(ss =>
                ss.TenantId == tenantId
                && ss.IsCurrent
                && ss.StudentId == s.Id
                && scopeIds.Contains(ss.SectionId))
            select s;

        var authorized = await authorizedQuery
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var membershipError = EnsureAllSubmittedStudentsAuthorized(
            submitted,
            authorized.Select(s => s.StudentNumber));
        if (membershipError != null)
            return (Array.Empty<Student>(), membershipError);

        return (authorized, null);
    }

    public static IReadOnlyList<string> NormalizeStudentNumbers(IEnumerable<string?> studentNumbers) =>
        studentNumbers
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Atomic section-scoped mark planning: either every submitted student yields a row, or none.
    /// Never silently drops unauthorized / unmapped students.
    /// </summary>
    public static (IReadOnlyList<TRow> Rows, string? Error) BuildAtomicMarkRows<TRow>(
        IReadOnlyList<string> submittedStudentNumbers,
        IReadOnlyList<Student> authorizedStudents,
        Func<Student, TRow?> rowFactory)
    {
        var submitted = NormalizeStudentNumbers(submittedStudentNumbers);
        var membershipError = EnsureAllSubmittedStudentsAuthorized(
            submitted,
            authorizedStudents.Select(s => s.StudentNumber));
        if (membershipError != null)
            return (Array.Empty<TRow>(), membershipError);

        if (authorizedStudents.Count != submitted.Count)
            return (Array.Empty<TRow>(), IncompleteAtomicWriteMessage);

        var byNumber = authorizedStudents.ToDictionary(s => s.StudentNumber, StringComparer.Ordinal);
        var rows = new List<TRow>(submitted.Count);
        foreach (var number in submitted)
        {
            if (!byNumber.TryGetValue(number, out var student))
                return (Array.Empty<TRow>(), UnauthorizedStudentsMessage);

            var row = rowFactory(student);
            if (row is null)
                return (Array.Empty<TRow>(), IncompleteAtomicWriteMessage);

            rows.Add(row);
        }

        if (rows.Count != submitted.Count)
            return (Array.Empty<TRow>(), IncompleteAtomicWriteMessage);

        return (rows, null);
    }

    /// <summary>
    /// Simulates atomic commit policy for tests: invalid ⇒ 0 committed; valid ⇒ all committed.
    /// </summary>
    public static int CountAtomicCommitOrZero(int submittedCount, int authorizedCount) =>
        submittedCount > 0 && submittedCount == authorizedCount ? submittedCount : 0;
}
