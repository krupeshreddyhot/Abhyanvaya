using Abhyanvaya.Application.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2A — Semester Group ownership write rules.</summary>
public sealed class SemesterGroupOwnershipRulesTests
{
    private static SemesterGroupOwnershipRules.GroupSnapshot Group(
        int id = 2, int tenantId = 1, int courseId = 10, bool deleted = false)
        => new(id, tenantId, courseId, deleted);

    [Fact]
    public void Rejects_Missing_Group()
    {
        var d = SemesterGroupOwnershipRules.EvaluateWrite(1, null, 10, null);
        Assert.False(d.Accepted);
        Assert.Contains("Group is required", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_Zero_Group()
    {
        var d = SemesterGroupOwnershipRules.EvaluateWrite(1, 0, 10, null);
        Assert.False(d.Accepted);
    }

    [Fact]
    public void Accepts_Valid_Group_And_Derives_Course()
    {
        var d = SemesterGroupOwnershipRules.EvaluateWrite(1, 2, 10, Group());
        Assert.True(d.Accepted);
        Assert.Equal(10, d.AlignedCourseId);
        Assert.Equal(2, d.AlignedGroupId);
    }

    [Fact]
    public void Rejects_Course_Mismatch()
    {
        var d = SemesterGroupOwnershipRules.EvaluateWrite(1, 2, 99, Group(courseId: 10));
        Assert.False(d.Accepted);
        Assert.Contains("Group does not belong to Course", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_Cross_Tenant_Group()
    {
        var d = SemesterGroupOwnershipRules.EvaluateWrite(1, 2, 10, Group(tenantId: 9));
        Assert.False(d.Accepted);
        Assert.Contains("tenant", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_Missing_Group_Entity()
    {
        var d = SemesterGroupOwnershipRules.EvaluateWrite(1, 2, 10, null);
        Assert.False(d.Accepted);
        Assert.Contains("Group not found", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepts_When_Client_Omits_Course_Hint()
    {
        var d = SemesterGroupOwnershipRules.EvaluateWrite(1, 2, 0, Group(courseId: 10));
        Assert.True(d.Accepted);
        Assert.Equal(10, d.AlignedCourseId);
    }
}

/// <summary>P1-4 Prompt 2A architecture contract guards.</summary>
public sealed class AiSchedCatalogTimetableP14Prompt2ASemesterWritePathGuardTests
{
    [Fact]
    public void Create_And_Update_DTOs_Require_GroupId()
    {
        Assert.Equal(typeof(int), typeof(Abhyanvaya.Application.DTOs.Semester.CreateSemesterRequest)
            .GetProperty("GroupId")!.PropertyType);
        Assert.Equal(typeof(int), typeof(Abhyanvaya.Application.DTOs.Semester.UpdateSemesterRequest)
            .GetProperty("GroupId")!.PropertyType);
        // Schema still nullable for legacy rows.
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
    }

    [Fact]
    public void Controller_Enforces_Group_Ownership_And_Group_Number_Uniqueness()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("SemesterGroupOwnershipRules", src, StringComparison.Ordinal);
        Assert.Contains("DuplicateExistsAsync", src, StringComparison.Ordinal);
        Assert.Contains("AlignedCourseId", src, StringComparison.Ordinal);
        Assert.Contains("IsLegacyCourseWide", src, StringComparison.Ordinal);
        Assert.DoesNotContain("request.GroupId.HasValue", src, StringComparison.Ordinal);
    }

    [Fact]
    public void AcademicTree_Operational_Null_Wildcard_Is_Retired()
    {
        var root = FindRepoRoot();
        var tree = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "AcademicTreeService.cs"));
        Assert.DoesNotContain("s.GroupId == null || s.GroupId == g.Id", tree, StringComparison.Ordinal);
        Assert.Contains("s.GroupId == g.Id", tree, StringComparison.Ordinal);
    }

    [Fact]
    public void Frozen_Catalog_And_Scheduling_Boundaries()
    {
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
        Assert.NotNull(typeof(SubjectAllocation).GetProperty(nameof(SubjectAllocation.DepartmentId)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.DepartmentId)));
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
    }

    [Fact]
    public void No_GroupId_NOT_NULL_Hardening_Migration_Introduced()
    {
        // Prompt 2A / 3J-A: additive journal/historical-archive migrations are allowed.
        // GroupId nullability hardening (NOT NULL) remains forbidden until Architect GO.
        var root = FindRepoRoot();
        var migrations = Path.Combine(root, "Abhyanvaya.Infrastructure", "Persistence", "Migrations");
        var hits = Directory.GetFiles(migrations, "*P1_4*")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                if (name.Contains("DispositionJournal", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.Contains("HistoricalArchive", StringComparison.OrdinalIgnoreCase)) return false;
                var text = File.ReadAllText(f);
                return text.Contains("AlterColumn", StringComparison.Ordinal)
                       && text.Contains("GroupId", StringComparison.Ordinal)
                       && text.Contains("nullable: false", StringComparison.Ordinal);
            })
            .ToList();
        Assert.Empty(hits);
    }

    [Fact]
    public void Ui_Requires_Group_And_Labels_Legacy()
    {
        var root = FindRepoRoot();
        var page = File.ReadAllText(Path.Combine(root, "abhyanvaya-ui", "src", "pages", "setup", "SemestersPage.tsx"));
        Assert.Contains("Legacy / Historical", page, StringComparison.Ordinal);
        Assert.Contains("Group is required", page, StringComparison.Ordinal);
        Assert.DoesNotContain("— None —", page, StringComparison.Ordinal);
        Assert.DoesNotContain("applies to the whole course", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Legacy / Course-wide", page, StringComparison.Ordinal);
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
