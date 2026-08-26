namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 8.1 — Discovery-only architecture guards (no production change).</summary>
public sealed class AiSchedCapPrompt81PublishReadinessUiDiscoveryGuardTests
{
    [Fact]
    public void Discovery_documentation_exists_and_is_discovery_only()
    {
        var path = Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_8_1_PUBLISH_READINESS_UI_DISCOVERY.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("DISCOVERY ONLY", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no production behavior changed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("publish-readiness", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API client", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SoftWarningsPanel", text);
        Assert.Contains("TimetableDesignerPage", text);
    }

    [Fact]
    public void Prompt81_did_not_change_PublishAsync_gate_or_readiness_service()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.Contains("ITimetablePublishReadinessService", lifecycle);
        Assert.Contains("EvaluatePublishReadinessAsync", lifecycle);
        Assert.Contains("PublishNotReadyException", lifecycle);

        var readiness = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.DoesNotContain("SaveChangesAsync", readiness);
        Assert.DoesNotContain("IUnitOfWork", readiness);
    }

    [Fact]
    public void Publish_API_route_and_readiness_GET_remain()
    {
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("publish-readiness", controller);
        Assert.Contains("{id:int}/publish", controller);
        Assert.Contains("CanPublishScheduling", controller);
        Assert.Contains("CanViewSchedulingTimetable", controller);
        Assert.Contains("PublishNotReadyException", controller);
    }

    [Fact]
    public void No_schema_migration_added_for_Prompt81()
    {
        // Discovery-only: no new migration files required. Guard documents expectation.
        var docs = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_8_1_PUBLISH_READINESS_UI_DISCOVERY.md"));
        Assert.Contains("Backend / schema", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deferred", docs, StringComparison.OrdinalIgnoreCase);
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
