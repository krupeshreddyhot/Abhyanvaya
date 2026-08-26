namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 2 — Contract confirmation (documentation only; no production changes).
/// </summary>
public sealed class AiSchedTg4APrompt2LegacyBridgeContractTests
{
    [Fact]
    public void Bridge_contract_document_exists_and_locks_SoT_projection()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_4A_PROMPT_2_LEGACY_SECTION_BRIDGE_CONTRACT.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("DESIGN CONTRACT ONLY", text);
        Assert.Contains("Source of truth", text);
        Assert.Contains("TeachingGroupSection", text);
        Assert.Contains("Projection", text);
        Assert.Contains("TeachingGroupId == null", text);
        Assert.Contains("Forbidden", text);
        Assert.Contains("no permanent production backfill", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STATUS = PASS", text);
        Assert.DoesNotContain("auto-create TeachingGroup on GET", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Contract_forbids_SA_inference_and_requires_reject_without_TG()
    {
        var text = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "docs", "AI_SCHED_TG_4A_PROMPT_2_LEGACY_SECTION_BRIDGE_CONTRACT.md"));
        Assert.Contains("Infer TG from SubjectAllocation", text);
        Assert.Contains("**Forbidden**", text);
        Assert.Contains("Assign a Teaching Group first", text);
        Assert.Contains("re-project all TimetableEntries", text);
    }

    [Fact]
    public void Prompt5_implemented_contract_Set_routes_through_TeachingGroup_boundary()
    {
        // Prompt 2 locked the contract; Prompt 5 applied it to SetTimetableSectionsAsync.
        var service = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Abhyanvaya.Application", "Academic", "SectionManagementService.cs"));
        var start = service.IndexOf("SetTimetableSectionsAsync", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var snippet = service.Substring(start, Math.Min(2200, service.Length - start));
        Assert.Contains("ReplaceSectionsAndProjectAsync", snippet);
        Assert.Contains("TeachingGroupId", snippet);
        Assert.DoesNotContain("new TimetableSection", snippet);
        Assert.DoesNotContain("CreateTeachingGroup", snippet);
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
