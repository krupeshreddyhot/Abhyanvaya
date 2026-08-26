namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 3 —
/// SubjectAllocation.DepartmentId is denormalized from Course.DepartmentId (Catalog SSOT).
/// </summary>
public static class SubjectAllocationCourseDepartmentRules
{
    public sealed record Decision(bool Accepted, int AlignedDepartmentId, string? Error);

    /// <summary>
    /// Validates optional client DepartmentId against authoritative Course.DepartmentId.
    /// Always returns Course.DepartmentId as the aligned scheduling value when accepted.
    /// </summary>
    public static Decision Evaluate(int? requestedDepartmentId, int? courseDepartmentId, bool courseFound)
    {
        if (!courseFound || courseDepartmentId is null or <= 0)
            return new Decision(false, 0, "Course not found.");

        if (requestedDepartmentId is > 0 && requestedDepartmentId.Value != courseDepartmentId.Value)
        {
            return new Decision(
                false,
                0,
                "SubjectAllocation Department must match the Course Department.");
        }

        return new Decision(true, courseDepartmentId.Value, null);
    }
}
