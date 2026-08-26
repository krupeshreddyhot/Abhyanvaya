namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B-A —
/// Student Course → Group → Semester ownership for write-path hardening (no inference).
/// </summary>
public static class StudentSemesterOwnershipRules
{
    public sealed record GroupSnapshot(int Id, int TenantId, int CourseId, bool IsDeleted);

    public sealed record SemesterSnapshot(
        int Id,
        int TenantId,
        int CourseId,
        int? GroupId,
        bool IsDeleted,
        bool IsHistoricalArchive);

    public sealed record Decision(bool Accepted, string? Error);

    /// <summary>
    /// Fail-closed validation for Student create/update/import.
    /// Requires a Group-specific Semester matching Student.GroupId and Student.CourseId.
    /// Does not infer a Semester when Group changes.
    /// </summary>
    public static Decision EvaluateWrite(
        int tenantId,
        int courseId,
        int groupId,
        int semesterId,
        GroupSnapshot? group,
        SemesterSnapshot? semester)
    {
        if (courseId <= 0)
            return Fail("Course is required.");
        if (groupId <= 0)
            return Fail("Group is required.");
        if (semesterId <= 0)
            return Fail("Semester is required.");

        if (group is null || group.IsDeleted)
            return Fail("Group not found.");
        if (group.Id != groupId)
            return Fail("Group not found.");
        if (group.TenantId != tenantId)
            return Fail("Group does not belong to tenant.");
        if (group.CourseId != courseId)
            return Fail("Group does not belong to Course.");

        if (semester is null || semester.IsDeleted)
            return Fail("Semester not found.");
        if (semester.Id != semesterId)
            return Fail("Semester not found.");
        if (semester.TenantId != tenantId)
            return Fail("Semester does not belong to tenant.");
        if (semester.IsHistoricalArchive)
            return Fail(OperationalSemesterRules.HistoricalRejectedMessage);
        if (semester.GroupId is null)
            return Fail("Semester must be Group-specific; legacy course-wide Semesters cannot be assigned to Students.");
        if (semester.GroupId.Value != groupId)
            return Fail("Semester does not belong to the selected Group.");
        if (semester.CourseId != courseId)
            return Fail("Semester does not belong to the selected Course.");

        return new Decision(true, null);
    }

    private static Decision Fail(string error) => new(false, error);
}
