using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 3A — Guard: single room-capacity semantics for ConflictEngine + SoftValidation.
/// </summary>
public sealed class AiSchedCapPrompt3AArchitectureGuardTests
{
    [Fact]
    public void Shared_RoomCapacityEvaluator_is_authoritative_and_registered()
    {
        var path = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "Capacity", "RoomCapacityEvaluator.cs");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("interface IRoomCapacityEvaluator", text);
        Assert.Contains("class RoomCapacityEvaluator", text);
        Assert.Contains("ComputeEffectiveCapacity", text);
        Assert.Contains("placement.Value > effective", text);

        var di = File.ReadAllText(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "DependencyInjection.cs"));
        Assert.Contains("IRoomCapacityEvaluator", di);
        Assert.Contains("RoomCapacityEvaluator", di);
    }

    [Fact]
    public void ConflictEngine_and_SoftValidation_both_use_RoomCapacityEvaluator_not_inline_math()
    {
        var roomRules = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "RoomConflictRules.cs"));
        Assert.Contains("RoomCapacityEvaluator.Evaluate", roomRules);
        Assert.DoesNotContain("room.Capacity * (1m", roomRules);
        Assert.DoesNotContain("RoomCapacityMarginPercent / 100m))", roomRules);

        var soft = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSoftValidationService.cs"));
        Assert.Contains("_roomCapacityEvaluator.Evaluate", soft);
        Assert.Contains("RoomCapacityMarginPercent", soft);
        Assert.DoesNotContain("room.Capacity < placement", soft);
        Assert.DoesNotContain("room.Capacity * (1m", soft);
    }

    [Fact]
    public void Room_capacity_remains_separate_from_TG_capacity_and_draft_stays_soft()
    {
        var evaluator = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Capacity", "RoomCapacityEvaluator.cs"));
        Assert.DoesNotContain("MaxTeachingCapacity", evaluator);
        Assert.DoesNotContain("TeachingGroup", evaluator);

        var lifecycle = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableLifecycleService.cs"));
        Assert.DoesNotContain("RoomCapacityEvaluator", lifecycle);
        Assert.DoesNotContain("PublishGate", lifecycle);

        var writers = Directory.EnumerateFiles(
                Path.Combine(FindRepoRoot(), "Abhyanvaya.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"new\s+TimetableSection\b(?!Dto)"))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Abhyanvaya.Infrastructure.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
