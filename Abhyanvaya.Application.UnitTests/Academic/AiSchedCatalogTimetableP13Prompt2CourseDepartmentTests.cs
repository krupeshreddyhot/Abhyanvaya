using Abhyanvaya.Application.Academic;
using Abhyanvaya.Domain.Entities;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 2 — Course.DepartmentId Option A invariants.</summary>
public sealed class AiSchedCatalogTimetableP13Prompt2CourseDepartmentTests
{
    private static CourseDepartmentAssociationRules.DepartmentSnapshot Dept(
        int id = 5, int tenantId = 1, int collegeId = 10) =>
        new(id, tenantId, collegeId, IsDeleted: false);

    private static CourseDepartmentAssociationRules.ProgramSnapshot Prog(
        int id = 7, int tenantId = 1, int collegeId = 10, int departmentId = 5) =>
        new(id, tenantId, collegeId, departmentId, IsDeleted: false);

    [Fact]
    public void Course_Entity_Requires_DepartmentId_And_Optional_ProgramId()
    {
        var c = new Course
        {
            Code = "BCOM",
            Name = "B.Com",
            DepartmentId = 5,
            ProgramId = null,
            TenantId = 1,
        };
        Assert.Equal(5, c.DepartmentId);
        Assert.Null(c.ProgramId);
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.Department)));
    }

    [Fact]
    public void Department_Required()
    {
        var d = CourseDepartmentAssociationRules.Evaluate(
            null, null, 1, null, null, enablePrograms: true);
        Assert.False(d.Accepted);
        Assert.Contains("required", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cross_Tenant_Department_Rejected()
    {
        var d = CourseDepartmentAssociationRules.Evaluate(
            5, Dept(5, tenantId: 99), courseTenantId: 1, null, null, true);
        Assert.False(d.Accepted);
        Assert.Contains("tenant", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_Department_Mismatch_Rejected()
    {
        var d = CourseDepartmentAssociationRules.Evaluate(
            5, Dept(5), 1, 7, Prog(7, departmentId: 99), enablePrograms: true);
        Assert.False(d.Accepted);
        Assert.Contains("match", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Valid_Program_And_Department_Accepted()
    {
        var d = CourseDepartmentAssociationRules.Evaluate(
            5, Dept(5), 1, 7, Prog(7, departmentId: 5), enablePrograms: true);
        Assert.True(d.Accepted);
    }

    [Fact]
    public void EnablePrograms_True_Allows_Null_Program()
    {
        var d = CourseDepartmentAssociationRules.Evaluate(
            5, Dept(5), 1, null, null, enablePrograms: true);
        Assert.True(d.Accepted);
    }

    [Fact]
    public void EnablePrograms_False_Rejects_ProgramId()
    {
        var d = CourseDepartmentAssociationRules.Evaluate(
            5, Dept(5), 1, 7, Prog(7), enablePrograms: false);
        Assert.False(d.Accepted);
    }

    [Fact]
    public void EnablePrograms_False_Accepts_Department_Only()
    {
        var d = CourseDepartmentAssociationRules.Evaluate(
            5, Dept(5), 1, null, null, enablePrograms: false);
        Assert.True(d.Accepted);
    }

    [Fact]
    public void Migration_Uses_Program_Then_Exactly_One_Tenant_Department()
    {
        var root = FindRepoRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Infrastructure", "Persistence", "Migrations",
            "20260822100000_AI_SCHED_CATALOG_P1_3_CourseDepartment.cs"));
        Assert.Contains("Program.DepartmentId", migration, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) = 1", migration, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"Course\"", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("SchedulingSubjectAllocation", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Architecture_No_SA_Or_TG_Changes_In_P13_Prompt2()
    {
        var root = FindRepoRoot();
        var write = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "CourseMasterWriteService.cs"));
        Assert.Contains("EnsureValidDepartmentOwnershipAsync", write, StringComparison.Ordinal);
        Assert.DoesNotContain("SubjectAllocation", write, StringComparison.Ordinal);
        Assert.DoesNotContain("TeachingGroup", write, StringComparison.Ordinal);

        var sa = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "SubjectAllocationService.cs"));
        Assert.DoesNotContain("Course.DepartmentId", sa, StringComparison.Ordinal);
        Assert.DoesNotContain("CourseDepartmentAssociationRules", sa, StringComparison.Ordinal);
    }

    [Fact]
    public void P1_2_Program_DepartmentId_Still_Present()
    {
        Assert.NotNull(typeof(Abhyanvaya.Domain.Entities.Academic.Program).GetProperty("DepartmentId"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Domain")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo root not found.");
    }
}
