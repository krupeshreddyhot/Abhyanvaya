using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 — Group-specific Semester resolution contract guards.
/// Discovery-only: asserts target architecture and frozen boundaries without changing production behavior.
/// </summary>
public sealed class AiSchedCatalogTimetableP14GroupSpecificSemesterContractGuardTests
{
    [Fact]
    public void Target_Semester_Ownership_Is_Group_Specific()
    {
        // Current schema still allows null (legacy); target contract requires Group association.
        var groupId = typeof(Semester).GetProperty(nameof(Semester.GroupId));
        Assert.NotNull(groupId);
        Assert.Equal(typeof(int?), groupId!.PropertyType);

        Assert.NotNull(typeof(Semester).GetProperty(nameof(Semester.CourseId)));
        Assert.NotNull(typeof(Group).GetProperty(nameof(Group.CourseId)));
    }

    [Fact]
    public void Group_Is_Associated_With_Course()
    {
        Assert.Equal(typeof(int), typeof(Group).GetProperty(nameof(Group.CourseId))!.PropertyType);
    }

    [Fact]
    public void Student_Semester_Resolution_Requires_Stored_Course_Group_Semester()
    {
        Assert.Equal(typeof(int), typeof(Student).GetProperty(nameof(Student.CourseId))!.PropertyType);
        Assert.Equal(typeof(int), typeof(Student).GetProperty(nameof(Student.GroupId))!.PropertyType);
        Assert.Equal(typeof(int), typeof(Student).GetProperty(nameof(Student.SemesterId))!.PropertyType);
    }

    [Fact]
    public void Null_GroupId_Is_Not_Target_Operational_Model_Documented()
    {
        var root = FindRepoRoot();
        var doc = File.ReadAllText(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_GROUP_SPECIFIC_SEMESTER_DISCOVERY.md"));
        Assert.Contains("GroupId = NULL", doc, StringComparison.Ordinal);
        Assert.Contains("valid target operational configuration", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one Semester row per Group", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TARGET UNIQUE KEY:", doc, StringComparison.Ordinal);
        Assert.Contains("TenantId + GroupId + Number", doc, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_Remains_Optional_And_EnablePrograms_Unchanged()
    {
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.ProgramId)));
        Assert.Equal(typeof(int?), typeof(Course).GetProperty(nameof(Course.ProgramId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
    }

    [Fact]
    public void Course_DepartmentId_Remains_Catalog_SSOT()
    {
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
    }

    [Fact]
    public void SubjectAllocation_And_TimetableEntry_Department_Architecture_Unchanged()
    {
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.DepartmentId)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.DepartmentId)));
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.SemesterId)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.SemesterId)));
    }

    [Fact]
    public void TeachingGroup_Architecture_And_No_TimetableEntry_SectionId()
    {
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.NotNull(typeof(TeachingGroup).GetProperty(nameof(TeachingGroup.GroupId)));
        Assert.NotNull(typeof(TeachingGroup).GetProperty(nameof(TeachingGroup.SemesterId)));
    }

    [Fact]
    public void Schema_Still_Allows_Nullable_GroupId_For_Legacy_Rows()
    {
        // P1-4 discovery: no NOT NULL yet. Prompt 2A enforces Group on writes; column remains nullable.
        var root = FindRepoRoot();
        var entity = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Domain", "Entities", "Semester.cs"));
        Assert.Contains("public int? GroupId", entity, StringComparison.Ordinal);

        var controller = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("SemesterGroupOwnershipRules", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Operational_Null_Group_Wildcard_Is_Retired()
    {
        var root = FindRepoRoot();
        var tree = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "AcademicTreeService.cs"));
        Assert.DoesNotContain("s.GroupId == null || s.GroupId == g.Id", tree, StringComparison.Ordinal);
        Assert.Contains("s.GroupId == g.Id", tree, StringComparison.Ordinal);

        var cascade = File.ReadAllText(Path.Combine(
            root, "abhyanvaya-ui", "src", "services", "setupService.ts"));
        Assert.DoesNotContain("s.groupId == null || Number(s.groupId) === gid", cascade, StringComparison.Ordinal);
        Assert.Contains("s.groupId != null && Number(s.groupId) === gid", cascade, StringComparison.Ordinal);

        var subjects = File.ReadAllText(Path.Combine(
            root, "abhyanvaya-ui", "src", "pages", "setup", "SubjectsPage.tsx"));
        Assert.DoesNotContain("s.groupId == null || Number(s.groupId)", subjects, StringComparison.Ordinal);
        Assert.Contains("filterSemestersForScope", subjects, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3I_LEGACY_WILDCARD_RETIREMENT.md")));
    }

    [Fact]
    public void P1_3_Prompt_4_Department_Alignment_Remains_Intact()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "Abhyanvaya.Application", "Scheduling", "TimetableEntryCourseDepartmentRules.cs")));
        Assert.True(File.Exists(Path.Combine(
            root, "Abhyanvaya.Application", "Scheduling", "SubjectAllocationCourseDepartmentRules.cs")));
    }

    [Fact]
    public void Attendance_And_CAP_Surfaces_Not_Altered_By_P1_4_Discovery()
    {
        // Guard: this prompt only adds contract tests + docs — no Attendance/CAP source edits required.
        var root = FindRepoRoot();
        Assert.True(Directory.Exists(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "Capacity")));
        Assert.True(File.Exists(Path.Combine(root, "Abhyanvaya.API", "Controllers", "AttendanceController.cs")));
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
