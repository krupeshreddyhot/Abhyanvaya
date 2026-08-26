using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A — architecture guards.
/// Proves no Group inference, no TG/TimetableSection/CAP/Publish changes, no schema hardening DDL.
/// </summary>
public sealed class AiSchedCatalogTimetableP14Prompt3JAHistoricalDispositionGuardTests
{
    [Fact]
    public void No_Automatic_Group_Inference_In_Disposition_Service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "LegacySemesterHistoricalDispositionService.cs"));
        Assert.DoesNotContain("OrderBy(g => g.Id).First", src, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AssignedGroupId = null", src, StringComparison.Ordinal);
        Assert.Contains("noGroupGuess=true", src, StringComparison.Ordinal);
        Assert.Contains("GroupIdMutated = false", src, StringComparison.Ordinal);
        Assert.DoesNotContain("semester.GroupId =", src, StringComparison.Ordinal);
    }

    [Fact]
    public void No_TG_Or_TimetableSection_Mutation_In_3JA()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "LegacySemesterHistoricalDispositionService.cs"));
        Assert.DoesNotContain("AddAsync(new TeachingGroup", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TimetableSections", src, StringComparison.Ordinal);
        Assert.Contains("noTgMutation=true", src, StringComparison.Ordinal);
        Assert.Contains("noTimetableSectionWrite=true", src, StringComparison.Ordinal);
        Assert.Contains("No TG/TGS mutation in this prompt", src, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Schema_Hardening_NotNull_Or_Unique_In_3JA_Migration()
    {
        var mig = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Infrastructure", "Persistence", "Migrations",
            "20260823190000_AI_SCHED_CATALOG_P1_4_Prompt3JA_SemesterHistoricalArchive.cs"));
        Assert.Contains("IsHistoricalArchive", mig, StringComparison.Ordinal);
        Assert.Contains("AddColumn", mig, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", mig, StringComparison.Ordinal);
        Assert.DoesNotContain("IsUnique", mig, StringComparison.Ordinal);
        Assert.Contains("no GroupId nullability change", mig, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Second_Legacy_Migration_Engine()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "LegacySemesterHistoricalDispositionService.cs"));
        Assert.Contains("ILegacySemesterFinalizationAuditService", src, StringComparison.Ordinal);
        Assert.DoesNotContain("class LegacySemesterMigrationClassifier", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Soft_Delete_Not_Used_As_Historical()
    {
        var entity = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Domain", "Entities", "Semester.cs"));
        Assert.Contains("IsHistoricalArchive", entity, StringComparison.Ordinal);
        var rules = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "OperationalSemesterRules.cs"));
        Assert.Contains("IsDeleted", rules, StringComparison.Ordinal);
        Assert.Contains("IsHistoricalArchive", rules, StringComparison.Ordinal);
    }

    [Fact]
    public void Cap_And_Publish_Surfaces_Untouched_By_3JA_Service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Academic", "LegacySemesterHistoricalDispositionService.cs"));
        Assert.DoesNotContain("PlacementSize", src, StringComparison.Ordinal);
        Assert.DoesNotContain("RoomCapacity", src, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Documentation_Exists()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(), "docs",
            "AI_SCHED_CATALOG_TIMETABLE_P1_4_PROMPT_3J_A_LEGACY_HISTORICAL_DISPOSITION.md")));
    }

    [Fact]
    public void Prompt3J_Not_Authorized_Flag_In_Dtos()
    {
        var dto = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "DTOs", "Academic", "LegacySemesterHistoricalDispositionDtos.cs"));
        Assert.Contains("Prompt3JAuthorized", dto, StringComparison.Ordinal);
        Assert.Contains("P1-4-3JA", dto, StringComparison.Ordinal);
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
