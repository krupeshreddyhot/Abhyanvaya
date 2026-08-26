using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 7 — Architecture guards.</summary>
public sealed class AiSchedCapPrompt7ArchitectureGuardTests
{
    [Fact]
    public void PublishAsync_consumes_ITimetablePublishReadinessService_before_mutation()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.Contains("ITimetablePublishReadinessService", lifecycle);
        Assert.Contains("EvaluatePublishReadinessAsync", lifecycle);
        Assert.Contains("PublishNotReadyException", lifecycle);

        var gateIndex = lifecycle.IndexOf("EvaluatePublishReadinessAsync", StringComparison.Ordinal);
        var mutateIndex = lifecycle.IndexOf("entity.Status = TimetableStatus.Published", StringComparison.Ordinal);
        var saveIndex = lifecycle.IndexOf("SaveChangesAsync", StringComparison.Ordinal);
        Assert.True(gateIndex > 0 && mutateIndex > gateIndex, "Readiness gate must run before status mutation.");
        Assert.True(saveIndex > gateIndex, "Readiness gate must run before SaveChanges.");

        // No duplicate capacity/conflict engines inside PublishAsync.
        Assert.DoesNotContain("PlacementSize", lifecycle);
        Assert.DoesNotContain("RoomCapacityEvaluator", lifecycle);
        Assert.DoesNotContain("ConflictEngine", lifecycle);
        Assert.DoesNotContain("TEACHING_GROUP_CAPACITY_EXCEEDED", lifecycle);
        Assert.DoesNotContain("new TeachingGroup", lifecycle);
        Assert.DoesNotContain("CreateTeachingGroup", lifecycle);
        Assert.DoesNotContain("IgnoreQueryFilters", lifecycle);
    }

    [Fact]
    public void PublishAsync_does_not_write_TimetableSection_or_Attendance_or_membership()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.DoesNotContain("TimetableSection", lifecycle);
        Assert.DoesNotContain("TeachingGroupSection", lifecycle);
        Assert.DoesNotContain("StudentSection", lifecycle);
        Assert.DoesNotContain("Attendance", lifecycle);
        Assert.DoesNotContain("ITimetableSectionProjector", lifecycle);
    }

    [Fact]
    public void No_second_ConflictEngine_or_capacity_stack()
    {
        var reg = ReadApp("Scheduling", "Conflicts", "ConflictRuleRegistration.cs");
        Assert.Equal(1, Regex.Matches(reg, @"AddScoped<\s*ConflictEngine").Count);

        var di = ReadApp("DependencyInjection.cs");
        Assert.Equal(1, Regex.Matches(di, @"IPlacementSizeResolver").Count);
        Assert.Equal(1, Regex.Matches(di, @"IRoomCapacityEvaluator").Count);
        Assert.Contains("ITimetablePublishReadinessService", di);
    }

    [Fact]
    public void Publish_API_returns_structured_readiness_on_block()
    {
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("PublishNotReadyException", controller);
        Assert.Contains("ex.Readiness", controller);
        Assert.Contains("publish-readiness", controller);
        Assert.Contains("CanPublishScheduling", controller);
    }

    [Fact]
    public void PublishNotReadyException_exists_and_carries_readiness_dto()
    {
        var path = Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Exceptions", "PublishNotReadyException.cs");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("TimetablePublishReadinessResultDto", text);
        Assert.Contains("Readiness", text);
    }

    [Fact]
    public void No_TimetableSection_writers_outside_projector()
    {
        var writers = Directory.EnumerateFiles(
                Path.Combine(FindRepoRoot(), "Abhyanvaya.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"new\s+TimetableSection\b(?!Dto)"))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);
    }

    [Fact]
    public void Documentation_exists()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_7_PUBLISH_GATE_ENFORCEMENT.md")));
    }

    private static string ReadApp(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot(), "Abhyanvaya.Application" }.Concat(parts).ToArray()));

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
