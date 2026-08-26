namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 4 —
/// TimetableEntry.DepartmentId is denormalized from Course.DepartmentId via SubjectAllocation.
/// </summary>
public static class TimetableEntryCourseDepartmentRules
{
    public sealed record Decision(bool Accepted, int AlignedDepartmentId, string? Error);

    /// <summary>
    /// Validates SubjectAllocation.DepartmentId against authoritative Course.DepartmentId.
    /// Returns Course.DepartmentId as the TimetableEntry scheduling denorm when accepted.
    /// </summary>
    public static Decision Evaluate(
        int subjectAllocationDepartmentId,
        int? courseDepartmentId,
        bool courseFound,
        int? requestedEntryDepartmentId = null)
    {
        if (!courseFound || courseDepartmentId is null or <= 0)
            return new Decision(false, 0, "Course not found.");

        if (subjectAllocationDepartmentId != courseDepartmentId.Value)
        {
            return new Decision(
                false,
                0,
                "SubjectAllocation Department must match the Course Department.");
        }

        if (requestedEntryDepartmentId is > 0
            && requestedEntryDepartmentId.Value != courseDepartmentId.Value)
        {
            return new Decision(
                false,
                0,
                "TimetableEntry Department must match the Course Department.");
        }

        return new Decision(true, courseDepartmentId.Value, null);
    }
}
