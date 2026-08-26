using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// P1-3 Prompt 1 contract updated after Prompt 2 implementation:
/// Course.DepartmentId is now present (Option A). Prompt 1 discovery doc remains historical.
/// </summary>
public sealed class AiSchedCatalogTimetableP13Prompt1ResolutionContractGuardTests
{
    [Fact]
    public void Discovery_Document_Exists_And_Recommends_Option_A()
    {
        var path = Path.Combine(FindRepoRoot(), "docs",
            "AI_SCHED_CATALOG_TIMETABLE_P1_3_COURSE_DEPARTMENT_RESOLUTION_DISCOVERY.md");
        Assert.True(File.Exists(path), "P1-3 Prompt 1 discovery document missing.");
        var text = File.ReadAllText(path);
        Assert.Contains("READ-ONLY DISCOVERY", text, StringComparison.Ordinal);
        Assert.Contains("Option A", text, StringComparison.Ordinal);
        Assert.Contains("catalog ownership SSOT", text, StringComparison.Ordinal);
        Assert.Contains("P1-2", text, StringComparison.Ordinal);
    }

    [Fact]
    public void P1_2_Program_DepartmentId_Remains_Frozen()
    {
        Assert.NotNull(typeof(Program).GetProperty(nameof(Program.DepartmentId)));
        Assert.NotNull(typeof(Program).GetProperty(nameof(Program.CollegeId)));
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
    }

    [Fact]
    public void Course_Now_Has_DepartmentId_Per_Option_A_Prompt2()
    {
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.ProgramId)));
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.DepartmentId)));
    }

    [Fact]
    public void Scheduling_Keeps_Independent_Operational_DepartmentId_Surfaces()
    {
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.DepartmentId)));
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.CourseId)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.DepartmentId)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
    }

    [Fact]
    public void Group_And_Semester_Remain_Course_Children_Without_DepartmentId()
    {
        Assert.NotNull(typeof(Group).GetProperty(nameof(Group.CourseId)));
        Assert.Null(typeof(Group).GetProperty("DepartmentId"));
        Assert.NotNull(typeof(Semester).GetProperty(nameof(Semester.CourseId)));
        Assert.Null(typeof(Semester).GetProperty("DepartmentId"));
    }

    [Fact]
    public void Prompt2_Write_Service_Owns_Department_Validation()
    {
        var root = FindRepoRoot();
        var write = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "CourseMasterWriteService.cs"));
        Assert.Contains("DepartmentId", write, StringComparison.Ordinal);
        Assert.Contains("EnsureValidDepartmentOwnershipAsync", write, StringComparison.Ordinal);
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
