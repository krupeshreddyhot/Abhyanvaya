using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 4 — TimetableEntry Department alignment guards.</summary>
public sealed class AiSchedCatalogTimetableP13Prompt4TimetableEntryDepartmentGuardTests
{
    [Fact]
    public void Rules_Accept_Aligned_SA_And_Course()
    {
        var d = TimetableEntryCourseDepartmentRules.Evaluate(5, 5, courseFound: true);
        Assert.True(d.Accepted);
        Assert.Equal(5, d.AlignedDepartmentId);
    }

    [Fact]
    public void Rules_Reject_SA_Course_Mismatch()
    {
        var d = TimetableEntryCourseDepartmentRules.Evaluate(2, 5, courseFound: true);
        Assert.False(d.Accepted);
        Assert.Contains("SubjectAllocation Department", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rules_Reject_Requested_Entry_Mismatch()
    {
        var d = TimetableEntryCourseDepartmentRules.Evaluate(5, 5, courseFound: true, requestedEntryDepartmentId: 9);
        Assert.False(d.Accepted);
        Assert.Contains("TimetableEntry Department", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rules_Reject_Missing_Course()
    {
        var d = TimetableEntryCourseDepartmentRules.Evaluate(5, null, courseFound: false);
        Assert.False(d.Accepted);
    }

    [Fact]
    public void Course_DepartmentId_Remains_Catalog_SSOT()
    {
        Assert.NotNull(typeof(Course).GetProperty(nameof(Course.DepartmentId)));
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.DepartmentId)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.DepartmentId)));
        Assert.NotNull(typeof(Program).GetProperty(nameof(Program.DepartmentId)));
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
    }

    [Fact]
    public void Service_Derives_Department_From_Course_Not_Client()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "TimetableService.cs"));
        Assert.Contains("ResolveCourseDepartmentIdAsync", src, StringComparison.Ordinal);
        Assert.Contains("TimetableEntryCourseDepartmentRules", src, StringComparison.Ordinal);
        Assert.Contains("RealignDepartmentFromCourseAsync", src, StringComparison.Ordinal);
        Assert.Contains("ApplyAllocationDenormalization(entry, allocation, roomId, courseDepartmentId)", src, StringComparison.Ordinal);
        Assert.DoesNotContain("entry.DepartmentId = allocation.DepartmentId", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Clone_And_Version_Paths_Realign_Department()
    {
        var root = FindRepoRoot();
        var clone = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "TimetableCloneService.cs"));
        var version = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "ScheduleVersionService.cs"));
        Assert.Contains("RealignDepartmentFromCourseAsync", clone, StringComparison.Ordinal);
        Assert.Contains("RealignDepartmentFromCourseAsync", version, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_Update_DTOs_Do_Not_Accept_Client_DepartmentId()
    {
        Assert.Null(typeof(Abhyanvaya.Application.DTOs.Scheduling.CreateTimetableEntryRequest)
            .GetProperty("DepartmentId"));
        Assert.Null(typeof(Abhyanvaya.Application.DTOs.Scheduling.UpdateTimetableEntryRequest)
            .GetProperty("DepartmentId"));
    }

    [Fact]
    public void No_TeachingGroup_Inference_Or_TimetableSection_Writer_Introduced()
    {
        var root = FindRepoRoot();
        var rules = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "TimetableEntryCourseDepartmentRules.cs"));
        Assert.DoesNotContain("TeachingGroup", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSection", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", rules, StringComparison.Ordinal);
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
    }

    [Fact]
    public void ApplyAllocationDenormalization_Still_Does_Not_Touch_TeachingGroupId()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Scheduling", "TimetableService.cs"));
        var denormStart = src.IndexOf("public static void ApplyAllocationDenormalization", StringComparison.Ordinal);
        Assert.True(denormStart >= 0);
        var brace = src.IndexOf('{', denormStart);
        var depth = 0;
        var denormEnd = brace;
        for (var i = brace; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    denormEnd = i;
                    break;
                }
            }
        }
        var denormBody = src.Substring(denormStart, denormEnd - denormStart + 1);
        Assert.DoesNotContain("TeachingGroupId", denormBody, StringComparison.Ordinal);
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
