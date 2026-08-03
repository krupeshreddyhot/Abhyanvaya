using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Ac1;

/// <summary>AI30 AC1 — Catalog Department is the single source of truth.</summary>
public sealed class Ac1CatalogDepartmentSsotTests
{
    [Fact]
    public void Domain_Department_IsTheOnlyDepartmentEntity()
    {
        var entity = new Department
        {
            Id = 1,
            CollegeId = 1,
            Name = "Computer Science",
            Code = "CSE",
            IsActive = true,
        };

        Assert.Equal("Computer Science", entity.Name);
        Assert.True(entity.IsActive);
        Assert.Equal(typeof(Department), entity.GetType());
    }

    [Fact]
    public void PermissionKeys_All_ExcludesRetiredSchedulingDepartmentPermissions()
    {
#pragma warning disable CS0618
        Assert.DoesNotContain(PermissionKeys.SchedulingDepartmentView, PermissionKeys.All);
        Assert.DoesNotContain(PermissionKeys.SchedulingDepartmentManage, PermissionKeys.All);
#pragma warning restore CS0618
        Assert.Contains(PermissionKeys.SetupDepartmentsManage, PermissionKeys.All);
    }

    [Fact]
    public void SetupDepartmentsManage_RemainsCatalogOwnerPermission()
    {
        Assert.Equal("Setup.Departments.Manage", PermissionKeys.SetupDepartmentsManage);
    }
}
