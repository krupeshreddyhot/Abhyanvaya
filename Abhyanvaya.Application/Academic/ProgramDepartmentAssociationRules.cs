namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-2 — pure rules for Program → Department association and tenant/College consistency.
/// </summary>
public static class ProgramDepartmentAssociationRules
{
    public sealed record DepartmentSnapshot(int Id, int TenantId, int CollegeId, bool IsDeleted, bool IsActive);

    public sealed record Decision(bool Accepted, string? Error);

    /// <summary>
    /// When Programs are enabled, DepartmentId is required for create/update.
    /// When disabled, Program CRUD is not required for catalog operation (Course remains top level);
    /// if a Program is still written, Department ownership remains required for data integrity.
    /// </summary>
    public static Decision Evaluate(
        bool enablePrograms,
        int? requestedDepartmentId,
        DepartmentSnapshot? department,
        int programTenantId,
        int programCollegeId)
    {
        if (requestedDepartmentId is null or <= 0)
        {
            if (enablePrograms)
                return new Decision(false, "Department is required when Programs are enabled.");
            return new Decision(false, "Department is required for a Program.");
        }

        if (department is null || department.IsDeleted)
            return new Decision(false, "Department not found.");

        if (department.TenantId != programTenantId)
            return new Decision(false, "Department must belong to the same tenant as the Program.");

        if (department.CollegeId != programCollegeId)
            return new Decision(false, "Department must belong to the same College as the Program.");

        if (department.Id != requestedDepartmentId.Value)
            return new Decision(false, "Department not found.");

        return new Decision(true, null);
    }
}
