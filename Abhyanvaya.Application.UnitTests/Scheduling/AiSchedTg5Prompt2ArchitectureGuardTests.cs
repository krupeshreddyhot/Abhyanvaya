using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.5 Prompt 2 — Architecture guards for Teaching Group management API boundary.
/// </summary>
public sealed class AiSchedTg5Prompt2ArchitectureGuardTests
{
    private static readonly Regex NewTimetableSectionEntity =
        new(@"new\s+TimetableSection\b(?!Dto)", RegexOptions.Compiled);

    private static readonly Regex NewTeachingGroupEntity =
        new(@"new\s+TeachingGroup\b(?!Section|Membership|Dto|Status|Type|Rules|Activity|Membership)", RegexOptions.Compiled);

    [Fact]
    public void TeachingGroupsController_uses_application_services_only()
    {
        var src = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TeachingGroupsController.cs");
        Assert.Contains("ITeachingGroupManagementApplicationService", src);
        Assert.Contains("ITeachingGroupSectionApplicationService", src);
        Assert.Contains("ReplaceSectionsAndProjectAsync", src);
        Assert.Contains("AddSectionAndProjectAsync", src);
        Assert.Contains("RemoveSectionAndProjectAsync", src);
        Assert.DoesNotContain("IApplicationDbContext", src);
        Assert.DoesNotContain("ApplicationDbContext", src);
        Assert.DoesNotContain("TimetableSections", src);
        Assert.DoesNotContain("SchedulingTeachingGroups", src);
        Assert.DoesNotContain("IgnoreQueryFilters", src);
        Assert.False(NewTimetableSectionEntity.IsMatch(src));
        Assert.False(NewTeachingGroupEntity.IsMatch(src));
    }

    [Fact]
    public void Management_service_never_infers_or_auto_creates_from_SubjectAllocation()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupManagementApplicationService.cs");
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        Assert.DoesNotContain("CreateIfMissing", src);
        Assert.DoesNotContain("FindFirstTeachingGroup", src);
        Assert.DoesNotContain("ResolveTeachingGroupFrom", src);
        Assert.DoesNotContain(".Single()", src);
        Assert.DoesNotContain(".SingleOrDefault()", src);
        Assert.DoesNotContain("TeachingGroups.Count == 0", src);
        Assert.DoesNotContain("new TimetableSection", src);
        Assert.Contains("SubjectAllocationId", src);
    }

    [Fact]
    public void Management_list_get_do_not_construct_TeachingGroup()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupManagementApplicationService.cs");
        var list = ExtractMethod(src, "ListBySubjectAllocationAsync");
        var get = ExtractMethod(src, "GetByIdAsync");
        Assert.DoesNotContain("new TeachingGroup", list);
        Assert.DoesNotContain("AddAsync", list);
        Assert.DoesNotContain("new TeachingGroup", get);
        Assert.DoesNotContain("AddAsync", get);
    }

    [Fact]
    public void Section_HTTP_path_projects_via_approved_methods()
    {
        var iface = Read("Abhyanvaya.Application", "Scheduling", "ITeachingGroupSectionApplicationService.cs");
        Assert.Contains("AddSectionAndProjectAsync", iface);
        Assert.Contains("RemoveSectionAndProjectAsync", iface);
        Assert.Contains("ReplaceSectionsAndProjectAsync", iface);

        var impl = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs");
        var addProj = ExtractMethod(impl, "AddSectionAndProjectAsync");
        var remProj = ExtractMethod(impl, "RemoveSectionAndProjectAsync");
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntriesAsync", addProj);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntriesAsync", remProj);
        Assert.False(NewTimetableSectionEntity.IsMatch(impl));
    }

    [Fact]
    public void Dedicated_TeachingGroup_RBAC_permissions_registered()
    {
        var keys = Read("Abhyanvaya.Domain", "Authorization", "PermissionKeys.cs");
        Assert.Contains("Scheduling.TeachingGroup.View", keys);
        Assert.Contains("Scheduling.TeachingGroup.Manage", keys);

        var policies = Read("Abhyanvaya.API", "Common", "AuthorizationPolicies.cs");
        Assert.Contains("CanViewSchedulingTeachingGroup", policies);
        Assert.Contains("CanManageSchedulingTeachingGroup", policies);

        var program = Read("Abhyanvaya.API", "Program.cs");
        Assert.Contains("CanViewSchedulingTeachingGroup", program);
        Assert.Contains("CanManageSchedulingTeachingGroup", program);
        Assert.Contains("SchedulingTeachingGroupView", program);
        Assert.Contains("SchedulingTeachingGroupManage", program);

        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TeachingGroupsController.cs");
        Assert.Contains("CanViewSchedulingTeachingGroup", controller);
        Assert.Contains("CanManageSchedulingTeachingGroup", controller);
    }

    [Fact]
    public void DI_registers_TeachingGroupManagementApplicationService()
    {
        var di = Read("Abhyanvaya.Application", "DependencyInjection.cs");
        Assert.Contains("ITeachingGroupManagementApplicationService", di);
        Assert.Contains("TeachingGroupManagementApplicationService", di);
    }

    [Fact]
    public void TeachingGroup_UI_files_remain_within_approved_TG5_TG6_surfaces()
    {
        // TG.5 Prompt 3 foundation + TG.6 membership UX + TG.6 timetable designer TG integration.
        var uiRoot = Path.Combine(FindRepoRoot(), "abhyanvaya-ui", "src");
        var allowedNameFragments = new[]
        {
            "teachingGroupService",
            "TeachingGroupsPage",
            "teachingGroupUi",
            "TeachingGroupMembership",
            "teachingGroupMembership",
            "AiSchedTg5Prompt3TeachingGroupUiGuard",
            "AiSchedTg6",
            "AiSchedCatalogTimetableP1",
            "timetableTeachingGroup",
            "aiSchedTg6",
        };

        foreach (var file in Directory.EnumerateFiles(uiRoot, "*.ts", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(uiRoot, "*.tsx", SearchOption.AllDirectories))
                     .Distinct())
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var isTgNamed = name.Contains("teachingGroup", StringComparison.OrdinalIgnoreCase)
                            || name.Contains("TeachingGroup", StringComparison.OrdinalIgnoreCase);
            if (!isTgNamed)
                continue;

            Assert.True(
                allowedNameFragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)),
                "Unexpected Teaching Group UI file outside approved TG.5/TG.6 surfaces: " + Rel(file));
        }

        // Designer may display/sync TG state but must not call assign/clear APIs directly.
        var designer = Read("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
        Assert.DoesNotContain("assignTeachingGroupToTimetableEntry", designer);
        Assert.DoesNotContain("clearTeachingGroupFromTimetableEntry", designer);
        Assert.DoesNotContain("setTimetableSections", designer);
    }

    [Fact]
    public void Prompt2_contract_document_exists()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_5_PROMPT_2_TEACHING_GROUP_MANAGEMENT_API.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("ITeachingGroupManagementApplicationService", text);
        Assert.Contains("Membership", text);
        Assert.Contains("TimetableSectionProjector", text);
    }

    private static string Rel(string fullPath)
        => Path.GetRelativePath(FindRepoRoot(), fullPath).Replace('\\', '/');

    private static string ExtractMethod(string src, string methodName)
    {
        var start = src.IndexOf(methodName + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, methodName + " not found");
        var brace = src.IndexOf('{', start);
        Assert.True(brace > start, methodName + " body not found");
        var depth = 0;
        for (var i = brace; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return src.Substring(start, i - start + 1);
            }
        }

        return src.Substring(start, Math.Min(3000, src.Length - start));
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
