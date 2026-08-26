using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1D Prompt 16A — read-only academic operational context access.
/// Policy allows Attendance / Sections / Timetable / Allocation / Program.View consumers.
/// Does not imply Program.Create/Edit/Delete/Manage.
/// </summary>
public static class AcademicOperationalContextAccess
{
    /// <summary>Existing permission keys that authorize GET breadcrumb/context.</summary>
    public static IReadOnlyList<string> AllowedPermissionKeys { get; } =
    [
        PermissionKeys.AttendanceView,
        PermissionKeys.AttendanceManage,
        PermissionKeys.SectionView,
        PermissionKeys.SectionAssignFaculty,
        PermissionKeys.SectionLifecycleView,
        PermissionKeys.SchedulingTimetableView,
        PermissionKeys.SchedulingTimetableManage,
        PermissionKeys.SchedulingView,
        PermissionKeys.SchedulingManage,
        PermissionKeys.AllocationRun,
        PermissionKeys.AllocationOperationsView,
        PermissionKeys.AllocationScenarioView,
        // Still allowed — not required.
        PermissionKeys.ProgramView,
    ];

    public static bool IsAllowed(IEnumerable<string> permissionKeys, string? role = null)
    {
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return true;

        var set = permissionKeys as ISet<string>
                  ?? permissionKeys.ToHashSet(StringComparer.Ordinal);
        return AllowedPermissionKeys.Any(set.Contains);
    }

    public static bool HasPermission(IEnumerable<System.Security.Claims.Claim> claims, string? role = null)
    {
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return true;

        var keys = claims
            .Where(c => string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value);
        return IsAllowed(keys, role);
    }
}
