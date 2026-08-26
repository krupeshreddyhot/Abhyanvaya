namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 8.2 — UI client contract does not change backend publish gate.</summary>
public sealed class AiSchedCapPrompt82PublishReadinessClientGuardTests
{
    [Fact]
    public void Documentation_exists()
    {
        var path = Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_8_2_PUBLISH_READINESS_UI_CLIENT_CONTRACT.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("isReady", text);
        Assert.Contains("isBlocking", text);
        Assert.Contains("getTimetablePublishReadiness", text);
    }

    [Fact]
    public void Backend_PublishAsync_gate_and_readiness_API_unchanged_by_Prompt82()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.Contains("EvaluatePublishReadinessAsync", lifecycle);
        Assert.Contains("PublishNotReadyException", lifecycle);

        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("publish-readiness", controller);
        Assert.Contains("BadRequest(ex.Readiness)", controller);
    }

    [Fact]
    public void UI_client_mirrors_backend_DTO_names()
    {
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "abhyanvaya-ui", "src", "services", "schedulingService.ts"));
        Assert.Contains("isReady", service);
        Assert.Contains("isBlocking", service);
        Assert.Contains("recommendedAction", service);
        Assert.Contains("timetableEntryId", service);
        Assert.Contains("getTimetablePublishReadiness", service);
        Assert.DoesNotContain("canPublish:", service);
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
