namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 1 discovery tests — superseded by TG.5 Prompt 2+ and TG.6.
/// Retained as historical supersession guards (not "feature absent" assertions).
/// </summary>
public sealed class AiSchedTg5Prompt1UxArchitectureDiscoveryTests
{
    [Fact]
    public void Discovery_document_exists_and_locks_TG4A_freeze()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_5_PROMPT_1_UX_ARCHITECTURE_DISCOVERY.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("DISCOVERY ONLY", text);
        Assert.Contains("STATUS: PASS", text);
        Assert.Contains("TeachingGroupSection", text);
        Assert.Contains("TimetableSectionProjector", text);
        Assert.Contains("FULL PASS — FROZEN", text);
        Assert.DoesNotContain("automatically create TeachingGroup on GET", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UI_TeachingGroup_surface_exists_after_TG5_TG6_delivery()
    {
        // Prompt 1 asserted absence; TG.5 Prompt 3 + TG.6 delivered management + timetable TG UI.
        var service = Path.Combine(FindRepoRoot(), "abhyanvaya-ui", "src", "services", "teachingGroupService.ts");
        var page = Path.Combine(FindRepoRoot(), "abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
        Assert.True(File.Exists(service));
        Assert.True(File.Exists(page));
    }

    [Fact]
    public void Assign_API_and_TeachingGroups_management_controller_coexist()
    {
        var timetables = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("entries/{entryId:int}/teaching-group", timetables);
        Assert.Contains("ITeachingGroupApplicationService", timetables);

        var apiControllers = Path.Combine(FindRepoRoot(), "Abhyanvaya.API", "Controllers");
        var tgControllers = Directory.EnumerateFiles(apiControllers, "*TeachingGroup*Controller*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetFileName(f))
            .ToList();
        Assert.Contains(tgControllers, n => n.Equals("TeachingGroupsController.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tgControllers, n => n.Equals("LegacyTimetableTeachingGroupConversionController.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Frozen_section_boundary_and_projector_still_present()
    {
        var iface = Read("Abhyanvaya.Application", "Scheduling", "ITeachingGroupSectionApplicationService.cs");
        Assert.Contains("ReplaceSectionsAndProjectAsync", iface);
        Assert.Contains("ITimetableSectionProjector", iface);

        var projector = Read("Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs");
        Assert.Contains(": ITimetableSectionProjector", projector);
        Assert.DoesNotContain("SaveChangesAsync", projector);
    }

    [Fact]
    public void PermissionKeys_define_TeachingGroup_RBAC_after_TG5_Prompt2()
    {
        // Prompt 1 asserted absence; TG.5 Prompt 2 registered dedicated permissions.
        var keys = Read("Abhyanvaya.Domain", "Authorization", "PermissionKeys.cs");
        Assert.Contains("SchedulingTeachingGroupView", keys);
        Assert.Contains("SchedulingTeachingGroupManage", keys);
        Assert.Contains("SchedulingTimetableManage", keys);
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
