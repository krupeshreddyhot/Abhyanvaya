namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 9 — Final integration baseline (discovery/acceptance only).</summary>
public sealed class AiSchedCapPrompt9FinalIntegrationBaselineGuardTests
{
    [Fact]
    public void Baseline_documentation_exists_and_recommends_pass()
    {
        var path = Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_9_FINAL_INTEGRATION_BASELINE.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("ACCEPTANCE / DISCOVERY ONLY", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no production behavior changed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Final recommendation: PASS", text);
        Assert.Contains("TimetableSectionProjector", text);
        Assert.Contains("EvaluatePublishReadinessAsync", text);
        Assert.Contains("PublishReadinessPanel", text);
        Assert.Contains("NOT EXECUTED", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STOP", text);
    }

    [Fact]
    public void Frozen_boundaries_still_hold_at_baseline()
    {
        var lifecycle = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableLifecycleService.cs"));
        Assert.Contains("EvaluatePublishReadinessAsync", lifecycle);
        Assert.Contains("PublishNotReadyException", lifecycle);

        var projector = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs"));
        Assert.DoesNotContain("SaveChangesAsync", projector);
        Assert.DoesNotContain("IUnitOfWork", projector);

        var readiness = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetablePublishReadinessService.cs"));
        Assert.DoesNotContain("SaveChangesAsync", readiness);
        Assert.DoesNotContain("IUnitOfWork", readiness);
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
