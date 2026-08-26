using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class LegacySemesterFinalizationClassifierTests
{
    [Fact]
    public void Classifies_Blocked_When_TeachingGroup_Refs()
    {
        var (d, _) = LegacySemesterFinalizationClassifier.Classify(new(
            3, 3, 2, false, 0, 2, 0, 0, 0, 0, 0, "RETAIN_LEGACY_PENDING_DECISION"));
        Assert.Equal(LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference, d);
    }

    [Fact]
    public void Classifies_Duplicate_Review()
    {
        var (d, _) = LegacySemesterFinalizationClassifier.Classify(new(
            4, 4, 2, true, 0, 0, 0, 0, 0, 0, 0, "DUPLICATE_REVIEW"));
        Assert.Equal(LegacySemesterFinalizationDisposition.DuplicateReview, d);
    }

    [Fact]
    public void Classifies_Historical_Retain_When_Empty_Multi_Group()
    {
        var (d, _) = LegacySemesterFinalizationClassifier.Classify(new(
            1, 1, 2, false, 0, 0, 0, 0, 0, 0, 0, "RETAIN_LEGACY_PENDING_DECISION"));
        Assert.Equal(LegacySemesterFinalizationDisposition.HistoricalRetain, d);
    }

    [Fact]
    public void Classifies_Safe_Single_Group_When_Empty()
    {
        var (d, _) = LegacySemesterFinalizationClassifier.Classify(new(
            7, 1, 1, false, 0, 0, 0, 0, 0, 0, 0, null));
        Assert.Equal(LegacySemesterFinalizationDisposition.SafeSingleGroupMapping, d);
    }
}

public sealed class AiSchedCatalogTimetableP14Prompt3DLegacySemesterFinalizationArchitectureGuardTests
{
    [Fact]
    public void Audit_Service_Is_Read_Only()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(
            root, "Abhyanvaya.Application", "Academic", "LegacySemesterFinalizationAuditService.cs"));
        Assert.Contains("AsNoTracking", src, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChanges", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteInTransactionAsync", src, StringComparison.Ordinal);
        Assert.Contains("NoMutationPerformed = true", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSection", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_Is_Read_Only_Finalization_Endpoint()
    {
        var root = FindRepoRoot();
        var src = File.ReadAllText(Path.Combine(root, "Abhyanvaya.API", "Controllers", "SemesterController.cs"));
        Assert.Contains("legacy-finalization-audit", src, StringComparison.Ordinal);
        Assert.Contains("ILegacySemesterFinalizationAuditService", src, StringComparison.Ordinal);
        Assert.Contains("CanManageSemesters", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Documentation_Exists()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3D_LEGACY_SEMESTER_FINALIZATION_AND_DB_HARDENING_DISCOVERY.md")));
        Assert.True(File.Exists(Path.Combine(
            root, "docs", "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3D_LEGACY_SEMESTER_FINALIZATION_AND_DB_HARDENING_CONTRACT.md")));
    }

    [Fact]
    public void Frozen_Boundaries_Unchanged()
    {
        Assert.Equal(typeof(int?), typeof(Semester).GetProperty(nameof(Semester.GroupId))!.PropertyType);
        Assert.NotNull(typeof(TimetableEntry).GetProperty(nameof(TimetableEntry.TeachingGroupId)));
        Assert.Null(typeof(TimetableEntry).GetProperty("SectionId"));
        Assert.Equal(typeof(int), typeof(Course).GetProperty(nameof(Course.DepartmentId))!.PropertyType);
        Assert.NotNull(typeof(TenantAcademicConfiguration).GetProperty(nameof(TenantAcademicConfiguration.EnablePrograms)));
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
