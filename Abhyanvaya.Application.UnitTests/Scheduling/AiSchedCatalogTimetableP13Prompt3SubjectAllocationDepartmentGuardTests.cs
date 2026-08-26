using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 3 — SA Department alignment guards.</summary>
public sealed class AiSchedCatalogTimetableP13Prompt3SubjectAllocationDepartmentGuardTests
{
    [Fact]
    public void Rules_Accept_Matching_Department()
    {
        var d = SubjectAllocationCourseDepartmentRules.Evaluate(5, 5, courseFound: true);
        Assert.True(d.Accepted);
        Assert.Equal(5, d.AlignedDepartmentId);
    }

    [Fact]
    public void Rules_Reject_Mismatch()
    {
        var d = SubjectAllocationCourseDepartmentRules.Evaluate(2, 5, courseFound: true);
        Assert.False(d.Accepted);
        Assert.Contains("must match", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rules_Derive_When_Client_Omits_Positive_Department()
    {
        var d = SubjectAllocationCourseDepartmentRules.Evaluate(0, 5, courseFound: true);
        Assert.True(d.Accepted);
        Assert.Equal(5, d.AlignedDepartmentId);
    }

    [Fact]
    public void Rules_Reject_Missing_Course()
    {
        var d = SubjectAllocationCourseDepartmentRules.Evaluate(5, null, courseFound: false);
        Assert.False(d.Accepted);
    }

    [Fact]
    public void Course_DepartmentId_Remains_Catalog_SSOT()
    {
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.DepartmentId)));
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.ProgramId)));
        Assert.NotNull(typeof(Program).GetProperty(nameof(Program.DepartmentId)));
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
    }

    [Fact]
    public void SubjectAllocation_Keeps_DepartmentId_As_Denorm_Column()
    {
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.DepartmentId)));
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.CourseId)));
    }

    [Fact]
    public void Service_Resolves_Department_From_Course_Not_As_Independent_SSOT()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "SubjectAllocationService.cs"));
        Assert.Contains("ResolveAlignedDepartmentIdAsync", src, StringComparison.Ordinal);
        Assert.Contains("SubjectAllocationCourseDepartmentRules", src, StringComparison.Ordinal);
        Assert.Contains("entity.DepartmentId = alignedDepartmentId", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TeachingGroup", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSectionProjector", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_Repairs_From_Course_Only()
    {
        var root = FindRepoRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Infrastructure", "Persistence", "Migrations",
            "20260822120000_AI_SCHED_CATALOG_P1_3_SubjectAllocationDepartmentAlign.cs"));
        Assert.Contains("SET \"DepartmentId\" = c.\"DepartmentId\"", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE \"Course\"", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void TimetableEntry_Still_Has_No_SectionId()
    {
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
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
