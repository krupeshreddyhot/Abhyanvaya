using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 6 — Architecture guards.</summary>
public sealed class AiSchedCapPrompt6ArchitectureGuardTests
{
    [Fact]
    public void Service_and_API_exist_and_are_read_only()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetablePublishReadinessService.cs")));
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "ITimetablePublishReadinessService.cs")));

        var service = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.DoesNotContain("SaveChangesAsync", service);
        Assert.DoesNotContain("_unitOfWork", service);
        Assert.DoesNotContain("IUnitOfWork", service);
        Assert.DoesNotContain("new TeachingGroup", service);
        Assert.DoesNotContain("CreateTeachingGroup", service);
        Assert.DoesNotContain("AssignTeachingGroup", service);
        Assert.DoesNotContain("ClearTeachingGroup", service);
        Assert.DoesNotContain("TimetableSection", service);
        Assert.DoesNotContain("StudentSection", service);
        Assert.DoesNotContain("IgnoreQueryFilters", service);
        Assert.Contains("IConflictAnalysisRunner", service);
        Assert.DoesNotContain("ConflictDetectionService", service);
    }

    [Fact]
    public void Reuses_single_ConflictEngine_and_capacity_stack()
    {
        var di = ReadApp("DependencyInjection.cs");
        Assert.Contains("ITimetablePublishReadinessService", di);
        Assert.Contains("IPlacementSizeResolver", di);
        Assert.Contains("IRoomCapacityEvaluator", di);

        var reg = ReadApp("Scheduling", "Conflicts", "ConflictRuleRegistration.cs");
        Assert.Contains("IConflictAnalysisRunner", reg);
        Assert.Equal(1, Regex.Matches(reg, @"AddScoped<\s*ConflictEngine").Count);
    }

    [Fact]
    public void PublishAsync_gate_is_owned_by_Prompt7_not_Prompt6_service()
    {
        // Prompt 6 delivered read-only readiness; Prompt 7 wires PublishAsync to the service.
        var service = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.DoesNotContain("SaveChangesAsync", service);
        Assert.DoesNotContain("IUnitOfWork", service);
        Assert.DoesNotContain("entity.Status = TimetableStatus.Published", service);

        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.Contains("ITimetablePublishReadinessService", lifecycle);
        Assert.Contains("EvaluatePublishReadinessAsync", lifecycle);
    }

    [Fact]
    public void API_route_uses_CanViewSchedulingTimetable()
    {
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("publish-readiness", controller);
        Assert.Contains("ITimetablePublishReadinessService", controller);
        Assert.Contains("CanViewSchedulingTimetable", controller);
    }

    [Fact]
    public void No_TimetableSection_writes_outside_projector()
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
            "docs", "AI_SCHED_CAP_PROMPT_6_PUBLISH_READINESS_APPLICATION_SERVICE.md")));
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
