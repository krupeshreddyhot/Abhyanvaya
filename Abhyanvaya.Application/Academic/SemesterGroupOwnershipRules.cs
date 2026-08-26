namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2A —
/// Operational Semester ownership is Group-specific. CourseId is validated denormalization of Group.CourseId.
/// </summary>
public static class SemesterGroupOwnershipRules
{
    public sealed record GroupSnapshot(int Id, int TenantId, int CourseId, bool IsDeleted);

    public sealed record Decision(bool Accepted, int AlignedCourseId, int AlignedGroupId, string? Error);

    public static Decision EvaluateWrite(
        int tenantId,
        int? requestedGroupId,
        int? requestedCourseId,
        GroupSnapshot? group)
    {
        if (requestedGroupId is null or <= 0)
            return Fail("Group is required for a Semester.");

        if (group is null || group.IsDeleted)
            return Fail("Group not found.");

        if (group.Id != requestedGroupId.Value)
            return Fail("Group not found.");

        if (group.TenantId != tenantId)
            return Fail("Group does not belong to tenant.");

        if (requestedCourseId is > 0 && requestedCourseId.Value != group.CourseId)
            return Fail("Group does not belong to Course.");

        return new Decision(true, group.CourseId, group.Id, null);
    }

    public static string DuplicateNumberMessage =>
        "A semester with this number already exists for this group.";

    private static Decision Fail(string error) => new(false, 0, 0, error);
}
