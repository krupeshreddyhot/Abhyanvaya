namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 2 — Course.DepartmentId ownership (Option A) + Program consistency.
/// </summary>
public static class CourseDepartmentAssociationRules
{
    public sealed record DepartmentSnapshot(int Id, int TenantId, int CollegeId, bool IsDeleted);

    public sealed record ProgramSnapshot(int Id, int TenantId, int CollegeId, int DepartmentId, bool IsDeleted);

    public sealed record Decision(bool Accepted, string? Error);

    /// <summary>
    /// Validates Course Department ownership. Program, when present, must match Department.
    /// EnablePrograms does not remove Department requirement; ProgramId may be null when enabled.
    /// </summary>
    public static Decision Evaluate(
        int? requestedDepartmentId,
        DepartmentSnapshot? department,
        int courseTenantId,
        int? requestedProgramId,
        ProgramSnapshot? program,
        bool enablePrograms)
    {
        if (requestedDepartmentId is null or <= 0)
            return Fail("Department is required for a Course.");

        if (department is null || department.IsDeleted)
            return Fail("Department not found.");

        if (department.TenantId != courseTenantId)
            return Fail("Department must belong to the same tenant as the Course.");

        if (department.Id != requestedDepartmentId.Value)
            return Fail("Department not found.");

        var programId = requestedProgramId is > 0 ? requestedProgramId : null;

        if (!enablePrograms)
        {
            if (programId is not null)
                return Fail("Program cannot be assigned when Programs are disabled.");
            return Ok();
        }

        if (programId is null)
            return Ok();

        if (program is null || program.IsDeleted)
            return Fail("Invalid Program.");

        if (program.TenantId != courseTenantId)
            return Fail("Program must belong to the same tenant as the Course.");

        if (program.CollegeId != department.CollegeId)
            return Fail("Program must belong to the same College as the Course Department.");

        if (program.DepartmentId != department.Id)
            return Fail("Course Department must match the Program Department.");

        return Ok();
    }

    private static Decision Ok() => new(true, null);
    private static Decision Fail(string error) => new(false, error);
}
