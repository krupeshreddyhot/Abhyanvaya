namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 5 — Architecture guards for membership resolver/mutation.</summary>
public sealed class AiSchedTg5Prompt5ArchitectureGuardTests
{
    [Fact]
    public void Membership_services_do_not_write_forbidden_surfaces()
    {
        var resolver = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupMembershipResolver.cs");
        var service = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupMembershipApplicationService.cs");

        foreach (var src in new[] { resolver, service })
        {
            Assert.DoesNotContain(".IgnoreQueryFilters", src);
            Assert.DoesNotContain("StudentSections.Add", src);
            Assert.DoesNotContain("new StudentSection", src);
            Assert.DoesNotContain("StudentSubjects.Add", src);
            Assert.DoesNotContain("new StudentSubject", src);
            Assert.DoesNotContain("new Attendance", src);
            Assert.DoesNotContain("Attendances.Add", src);
            Assert.DoesNotContain("new TimetableSection", src);
            Assert.DoesNotContain("TimetableSections.Add", src);
            Assert.DoesNotContain("CreateTeachingGroupIfMissing", src);
            Assert.DoesNotContain("FindFirstTeachingGroup", src);
        }

        Assert.DoesNotContain("SaveChanges", resolver);
        Assert.DoesNotContain("AddAsync", resolver);
    }

    [Fact]
    public void Controller_membership_mutations_use_application_service()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TeachingGroupsController.cs");
        Assert.Contains("ITeachingGroupMembershipApplicationService", controller);
        Assert.Contains("resolved-members", controller);
        Assert.Contains("AddMembersAsync", controller);
        Assert.Contains("ReplaceMembershipsAsync", controller);
        Assert.Contains("RemoveMemberAsync", controller);
        Assert.DoesNotContain("SchedulingTeachingGroupMemberships", controller);
        Assert.DoesNotContain("IgnoreQueryFilters", controller);
    }

    [Fact]
    public void DI_registers_resolver_and_membership_service()
    {
        var di = Read("Abhyanvaya.Application", "DependencyInjection.cs");
        Assert.Contains("ITeachingGroupMembershipResolver", di);
        Assert.Contains("ITeachingGroupMembershipApplicationService", di);
    }

    [Fact]
    public void UI_membership_mutation_deferred_from_Prompt_5_delivered_in_TG6()
    {
        // Prompt 5 shipped API only; TG.6 Prompt 3 delivered membership UX.
        var page = Read("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
        var panel = Read("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx");
        Assert.Contains("TeachingGroupMembershipPanel", page);
        Assert.Contains("getResolvedTeachingGroupMembers", page);
        Assert.Contains("addTeachingGroupMembersWithReload", panel);
        Assert.DoesNotContain("replaceTeachingGroupMemberships(", page);
    }

    [Fact]
    public void Prompt5_implementation_document_exists()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_5_PROMPT_5_MEMBERSHIP_RESOLVER_MUTATION_IMPLEMENTATION.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("Model B", text);
        Assert.Contains("ITeachingGroupMembershipResolver", text);
        Assert.Contains("MaxTeachingCapacity", text);
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(parts).ToArray()));

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
