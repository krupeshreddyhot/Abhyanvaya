using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.5 Prompt 5A — Architecture + RBAC guards for membership integrity.</summary>
public sealed class AiSchedTg5Prompt5AArchitectureGuardTests
{
    [Fact]
    public void Resolver_has_no_mutation_side_effects_in_source()
    {
        var resolver = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupMembershipResolver.cs");
        Assert.DoesNotContain("SaveChanges", resolver);
        Assert.DoesNotContain("AddAsync", resolver);
        Assert.DoesNotContain("IgnoreQueryFilters", resolver);
        Assert.DoesNotContain("new TeachingGroup", resolver);
        Assert.DoesNotContain("new TeachingGroupMembership", resolver);
        Assert.DoesNotContain("new StudentSection", resolver);
        Assert.DoesNotContain("new StudentSubject", resolver);
        Assert.DoesNotContain("new Attendance", resolver);
        Assert.DoesNotContain("new TimetableSection", resolver);
        Assert.DoesNotContain("new ApplicationDbContext", resolver);
        Assert.DoesNotContain("Abhyanvaya.Infrastructure", resolver);
        Assert.Contains("IApplicationDbContext", resolver);
    }

    [Fact]
    public void Membership_service_uses_resolved_peer_exclusion()
    {
        var service = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupMembershipApplicationService.cs");
        Assert.Contains("EnsureExclusionAgainstResolvedPeersAsync", service);
        Assert.Contains("ConcurrencyConflictException", service);
        Assert.Contains("ValidateProposedStateAsync", service);
        Assert.DoesNotContain("IgnoreQueryFilters", service);
        Assert.DoesNotContain("new TimetableSection", service);
        Assert.DoesNotContain("new Attendance", service);
        Assert.DoesNotContain("new StudentSection", service);
    }

    [Fact]
    public void Controller_membership_mutations_require_Manage_policy()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TeachingGroupsController.cs");
        Assert.Contains("CanViewSchedulingTeachingGroup", controller);
        Assert.Contains("CanManageSchedulingTeachingGroup", controller);

        Assert.Matches(
            new Regex(@"HttpPost\(""\{id:int\}/memberships""\)[\s\S]*?CanManageSchedulingTeachingGroup", RegexOptions.Multiline),
            controller);
        Assert.Matches(
            new Regex(@"HttpPut\(""\{id:int\}/memberships""\)[\s\S]*?CanManageSchedulingTeachingGroup", RegexOptions.Multiline),
            controller);
        Assert.Matches(
            new Regex(@"HttpDelete\(""\{id:int\}/memberships/\{studentId:int\}""\)[\s\S]*?CanManageSchedulingTeachingGroup", RegexOptions.Multiline),
            controller);

        // Reads inherit class-level View; must not require Manage on GET membership endpoints.
        var getMemberships = Regex.Match(
            controller,
            @"HttpGet\(""\{id:int\}/memberships""\)[\s\S]*?public async Task",
            RegexOptions.Multiline).Value;
        Assert.False(string.IsNullOrEmpty(getMemberships));
        Assert.DoesNotContain("CanManageSchedulingTeachingGroup", getMemberships);

        var getResolved = Regex.Match(
            controller,
            @"HttpGet\(""\{id:int\}/resolved-members""\)[\s\S]*?public async Task",
            RegexOptions.Multiline).Value;
        Assert.False(string.IsNullOrEmpty(getResolved));
        Assert.DoesNotContain("CanManageSchedulingTeachingGroup", getResolved);
    }

    [Fact]
    public void Controller_does_not_touch_EF_membership_or_projection_sets()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TeachingGroupsController.cs");
        Assert.DoesNotContain("SchedulingTeachingGroupMemberships", controller);
        Assert.DoesNotContain("new TeachingGroupMembership", controller);
        Assert.DoesNotContain("IgnoreQueryFilters", controller);
        Assert.Contains("ITeachingGroupMembershipApplicationService", controller);
        Assert.Contains("Conflict(", controller);
    }

    [Fact]
    public void Program_registers_TeachingGroup_View_and_Manage_policies()
    {
        var program = Read("Abhyanvaya.API", "Program.cs");
        Assert.Contains("CanViewSchedulingTeachingGroup", program);
        Assert.Contains("CanManageSchedulingTeachingGroup", program);
        Assert.Contains("SchedulingTeachingGroupView", program);
        Assert.Contains("SchedulingTeachingGroupManage", program);
    }

    [Fact]
    public void Resolved_member_dto_omits_IsExcludedFromBase()
    {
        var dto = Read("Abhyanvaya.Application", "DTOs", "Scheduling", "TeachingGroupMembershipMutationDtos.cs");
        Assert.DoesNotContain("IsExcludedFromBase", dto);
        Assert.Contains("ResolvedTeachingGroupMemberDto", dto);
        Assert.Contains("TeachingGroupMemberProvenance", dto);
    }

    [Fact]
    public void UI_membership_editor_delivered_after_TG6_Prompt3()
    {
        // 5A hardened API; membership editor UI arrived in TG.6 Prompt 3.
        var page = Read("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
        var panel = Read("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx");
        Assert.Contains("TeachingGroupMembershipPanel", page);
        Assert.Contains("addTeachingGroupMembersWithReload", panel);
        Assert.DoesNotContain("replaceTeachingGroupMemberships(", page);
    }

    [Fact]
    public void Prompt5A_document_exists()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_5_PROMPT_5A_MEMBERSHIP_INTEGRITY_HARDENING.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("ExclusionGroupKey", text);
        Assert.Contains("PostgreSQL", text);
        Assert.Contains("Model B", text);
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
