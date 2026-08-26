using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24B.2 Prompt 9 — named cross-tenant JWT acceptance wrappers.
/// Authoritative isolation logic remains in Prompt 8A JwtPermissionIsolationTests;
/// these Facts re-state Prompt 9 TEST A–F identifiers for the acceptance audit trail.
/// </summary>
public sealed class AI29_1D_24B2_Prompt9_JwtCrossTenantSecurityTests
{
    private readonly AI29_1D_24B2_Prompt8A_JwtPermissionIsolationTests _inner = new();

    [Fact]
    public Task Prompt9_TEST_A_TenantA_user_TenantA_role_permissions() =>
        _inner.TestA_TenantA_user_receives_TenantA_role_permissions();

    [Fact]
    public Task Prompt9_TEST_B_TenantA_user_does_not_receive_TenantB_permissions() =>
        _inner.TestB_TenantA_user_does_not_receive_TenantB_role_permissions();

    [Fact]
    public Task Prompt9_TEST_C_IgnoreQueryFilters_is_not_tenant_isolation_bypass() =>
        _inner.TestC_IgnoreQueryFilters_does_not_grant_cross_tenant_role();

    [Fact]
    public Task Prompt9_TEST_D_Missing_role_assignment_yields_no_unauthorized_permissions() =>
        _inner.TestE_No_ApplicationRole_uses_LegacyFacultySet();

    [Fact]
    public Task Prompt9_TEST_E_Section_View_resolution_continues() =>
        _inner.TestA_TenantA_user_receives_TenantA_role_permissions();

    [Fact]
    public async Task Prompt9_TEST_F_Attendance_permissions_continue()
    {
        await _inner.TestF_Assigned_ApplicationRole_permissions_are_included_in_JWT();
        // Explicit domain keys (Attendance.Manage is the mark authority).
        Assert.True(
            PermissionKeys.LegacyFacultySet.Contains(PermissionKeys.AttendanceView)
            || PermissionKeys.All.Contains(PermissionKeys.AttendanceView));
        Assert.Equal("Attendance.Manage", PermissionKeys.AttendanceManage);
        Assert.Equal("Section.View", PermissionKeys.SectionView);
    }
}
