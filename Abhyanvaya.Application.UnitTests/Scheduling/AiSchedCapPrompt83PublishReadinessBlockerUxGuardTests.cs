namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 8.3 — UI blocker UX does not alter backend publish gate.</summary>
public sealed class AiSchedCapPrompt83PublishReadinessBlockerUxGuardTests
{
    [Fact]
    public void Documentation_exists()
    {
        var path = Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_8_3_PUBLISH_READINESS_BLOCKER_UX.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("PublishReadinessPanel", text);
        Assert.Contains("isBlocking", text);
        Assert.Contains("NOT EXECUTED", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Backend_publish_gate_unchanged()
    {
        var lifecycle = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableLifecycleService.cs"));
        Assert.Contains("EvaluatePublishReadinessAsync", lifecycle);
        Assert.Contains("PublishNotReadyException", lifecycle);

        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.Contains("publish-readiness", controller);
        Assert.Contains("BadRequest(ex.Readiness)", controller);
    }

    [Fact]
    public void UI_panel_and_pages_exist()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "abhyanvaya-ui", "src", "pages", "setup", "scheduling", "PublishReadinessPanel.tsx")));
        var designer = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "abhyanvaya-ui", "src", "pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx"));
        Assert.Contains("PublishReadinessPanel", designer);
        Assert.Contains("getTimetablePublishReadiness", designer);
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
