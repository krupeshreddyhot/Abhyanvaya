namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 3 — UI foundation must not bypass TG.4A / Prompt 2 contracts.</summary>
public sealed class AiSchedTg5Prompt3UiArchitectureGuardTests
{
    [Fact]
    public void TeachingGroups_UI_does_not_call_TimetableSection_mutation_APIs()
    {
        var page = ReadUi("src", "pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
        var service = ReadUi("src", "services", "teachingGroupService.ts");

        Assert.DoesNotContain("setTimetableSections", page);
        Assert.DoesNotContain("/timetable/", page);
        Assert.DoesNotContain("TimetableSections.Add", page);
        Assert.Contains("addTeachingGroupSection", page);
        Assert.Contains("removeTeachingGroupSection", page);
        Assert.Contains("/scheduling/teaching-groups", service);
        Assert.DoesNotContain("setTimetableSections", service);
    }

    [Fact]
    public void TeachingGroups_UI_does_not_auto_create_or_infer_from_SubjectAllocation()
    {
        var page = ReadUi("src", "pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
        var helpers = ReadUi("src", "pages", "setup", "scheduling", "teachingGroupUi.ts");
        Assert.Contains("never created automatically", page);
        Assert.Contains("shouldAutoCreateTeachingGroupFromSubjectAllocation", helpers);
        Assert.Contains("=> false", helpers);
        Assert.DoesNotContain("createTeachingGroupIfMissing", page);
        Assert.DoesNotContain("FindFirstTeachingGroup", page);
    }

    [Fact]
    public void Membership_mutation_is_not_invented_in_UI_client()
    {
        var service = ReadUi("src", "services", "teachingGroupService.ts");
        Assert.Contains("getTeachingGroupMemberships", service);
        Assert.DoesNotContain("createTeachingGroupMembership", service);
        Assert.DoesNotContain("updateTeachingGroupMembership", service);
        Assert.DoesNotContain("deleteTeachingGroupMembership", service);
        Assert.DoesNotContain("putTeachingGroupMembership", service);
    }

    [Fact]
    public void Discovery_and_acceptance_documents_exist()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "docs", "AI_SCHED_TG_5_PROMPT_3_UI_DISCOVERY.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "AI_SCHED_TG_5_PROMPT_3_UI_FOUNDATION_ACCEPTANCE.md")));
    }

    private static string ReadUi(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot(), "abhyanvaya-ui" }.Concat(parts).ToArray()));

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
