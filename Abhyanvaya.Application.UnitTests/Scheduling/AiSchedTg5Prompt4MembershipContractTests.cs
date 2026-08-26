namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 4 — Contract documentation & non-implementation guards.
/// Prompt 4 must not ship membership mutation code.
/// </summary>
public sealed class AiSchedTg5Prompt4MembershipContractTests
{
    [Fact]
    public void Prompt4_deliverable_documents_exist_with_required_decisions()
    {
        var root = FindRepoRoot();
        var required = new Dictionary<string, string[]>
        {
            ["AI_SCHED_TG_5_PROMPT_4_MEMBERSHIP_DISCOVERY.md"] = ["UNDEFINED", "TeachingGroupMembership", "PASS"],
            ["AI_SCHED_TG_5_PROMPT_4_MEMBERSHIP_SOURCE_OF_TRUTH.md"] = ["TimetableSection", "StudentSection", "Resolved"],
            ["AI_SCHED_TG_5_PROMPT_4_MEMBERSHIP_SEMANTICS.md"] = ["Model B", "Hybrid", "ExclusionGroupKey", "ResolvedStudentCount"],
            ["AI_SCHED_TG_5_PROMPT_4_MEMBERSHIP_MUTATION_CONTRACT.md"] = ["ITeachingGroupMembershipApplicationService", "AddMembers", "DO NOT IMPLEMENT"],
            ["AI_SCHED_TG_5_PROMPT_4_MEMBERSHIP_API_CONTRACT.md"] = ["resolved-members", "memberships", "DO NOT IMPLEMENT"],
            ["AI_SCHED_TG_5_PROMPT_4_MEMBERSHIP_ACCEPTANCE_MATRIX.md"] = ["Hybrid", "No Attendance mutation", "ExclusionGroupKey"],
            ["AI_SCHED_TG_5_PROMPT_4_FINAL_ARCHITECTURE_DECISION.md"] = ["CONDITIONAL PASS", "Model B", "READY FOR IMPLEMENTATION"],
        };

        foreach (var (file, needles) in required)
        {
            var path = Path.Combine(root, "docs", file);
            Assert.True(File.Exists(path), path);
            var text = File.ReadAllText(path);
            foreach (var needle in needles)
                Assert.Contains(needle, text);
        }
    }

    [Fact]
    public void Prompt4_contract_forbade_mutation_until_Prompt5_which_now_owns_the_service()
    {
        var appRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application");
        var hits = Directory.EnumerateFiles(appRoot, "*Membership*Application*.cs", SearchOption.AllDirectories).ToList();
        Assert.Contains(hits, h => h.Replace('\\', '/').Contains("TeachingGroupMembershipApplicationService.cs"));

        var controller = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "Abhyanvaya.API", "Controllers", "Scheduling", "TeachingGroupsController.cs"));
        Assert.Contains("GetMemberships", controller);
        Assert.Contains("resolved-members", controller);
        Assert.Contains("AddMembersAsync", controller);
    }

    [Fact]
    public void Prompt3_UI_membership_panel_delivered_in_TG6_Prompt3()
    {
        // Prompt 4 asserted membership UI not yet available; TG.6 Prompt 3 delivered Explicit/Hybrid panel.
        var page = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupsPage.tsx"));
        var panel = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx"));
        Assert.Contains("TeachingGroupMembershipPanel", page);
        Assert.Contains("getResolvedTeachingGroupMembers", page);
        Assert.Contains("addTeachingGroupMembersWithReload", panel);
        Assert.DoesNotContain("replaceTeachingGroupMemberships(", page);
    }

    [Fact]
    public void Hybrid_Model_B_is_explicitly_chosen_over_Model_A()
    {
        var semantics = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "docs", "AI_SCHED_TG_5_PROMPT_4_MEMBERSHIP_SEMANTICS.md"));
        Assert.Contains("Model B", semantics);
        Assert.Contains("ExplicitExcludes", semantics);
        Assert.Contains("Rejected:", semantics);
        Assert.Contains("Model A", semantics);
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
