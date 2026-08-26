using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.6 Final — Consolidated architecture guard suite (Prompt 14).
/// Proves forbidden paths remain closed and required SoT/projection boundaries remain open.
/// </summary>
public sealed class AiSchedTg6FinalArchitectureGuardTests
{
    private static readonly Regex NewTimetableSectionEntity =
        new(@"new\s+TimetableSection\b(?!Dto)", RegexOptions.Compiled);

    [Fact]
    public void Forbidden_controller_and_TimetableService_do_not_write_TimetableSection()
    {
        var apiRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.API", "Controllers");
        foreach (var file in Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(file);
            Assert.False(NewTimetableSectionEntity.IsMatch(src), Rel(file));
            Assert.DoesNotContain("TimetableSections.Add", src);
            Assert.DoesNotContain("Set<TimetableSection>", src);
        }

        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableCloneService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "ScheduleVersionService.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.False(NewTimetableSectionEntity.IsMatch(src), relative);
            Assert.DoesNotContain("ITimetableSectionProjector", src);
        }
    }

    [Fact]
    public void Required_TimetableSectionProjector_is_sole_Application_entity_writer()
    {
        var appRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application");
        var hits = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => NewTimetableSectionEntity.IsMatch(File.ReadAllText(f)))
            .Select(Rel)
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(
            new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" },
            hits);
    }

    [Fact]
    public void Required_TeachingGroupSection_SoT_and_projector_wiring()
    {
        var sections = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs");
        Assert.Contains("ReplaceSectionsAndProjectAsync", sections);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntriesAsync", sections);
        Assert.False(NewTimetableSectionEntity.IsMatch(sections));

        var legacy = ExtractMethod(
            Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs"),
            "SetTimetableSectionsAsync");
        Assert.Contains("ReplaceSectionsAndProjectAsync", legacy);
        Assert.False(NewTimetableSectionEntity.IsMatch(legacy));
    }

    [Fact]
    public void Required_compatible_query_and_dedicated_assign_clear_boundary()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("GetCompatibleTeachingGroups", controller);
        Assert.Contains("ICompatibleTeachingGroupQueryService", controller);
        Assert.Contains("AssignTeachingGroup", controller);
        Assert.Contains("ClearTeachingGroup", controller);
        Assert.Contains("ITeachingGroupApplicationService", controller);

        var dto = Read("Abhyanvaya.Application", "DTOs", "Scheduling", "TimetableDtos.cs");
        Assert.DoesNotContain("TeachingGroupId", ExtractType(dto, "CreateTimetableEntryRequest"));
        Assert.DoesNotContain("TeachingGroupId", ExtractType(dto, "UpdateTimetableEntryRequest"));
        Assert.DoesNotContain("TeachingGroupId", ExtractType(dto, "UpsertTimetableEntryRequest"));
        Assert.Contains("TeachingGroupId", ExtractType(dto, "AssignTeachingGroupToTimetableEntryRequest"));
    }

    [Fact]
    public void Forbidden_IgnoreQueryFilters_on_TG_Scheduling_paths()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TeachingGroupManagementApplicationService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TeachingGroupMembershipApplicationService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TeachingGroupMembershipResolver.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "CompatibleTeachingGroupQueryService.cs"),
                     Path.Combine("Abhyanvaya.API", "Controllers", "Scheduling", "TeachingGroupsController.cs"),
                     Path.Combine("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.DoesNotContain(".IgnoreQueryFilters", src, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Forbidden_UI_paths_do_not_write_TimetableSection_Attendance_or_infer_SA_to_TG()
    {
        var uiFiles = new[]
        {
            Path.Combine("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx"),
            Path.Combine("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx"),
            Path.Combine("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "timetable", "timetableTeachingGroupAssignmentActions.ts"),
            Path.Combine("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupsPage.tsx"),
            Path.Combine("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx"),
        };

        foreach (var relative in uiFiles)
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.DoesNotContain("setTimetableSections", src);
            Assert.DoesNotContain("/attendance", src, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("createTeachingGroupIfMissing", src, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("inferTeachingGroup", src, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("StudentSection", src);
        }

        var actions = Read("abhyanvaya-ui", "src", "pages", "setup", "scheduling", "timetable", "timetableTeachingGroupAssignmentActions.ts");
        Assert.Contains("listCompatibleTeachingGroupsForTimetableEntry", actions);
        Assert.Contains("assignTeachingGroupToTimetableEntry", actions);
        Assert.Contains("clearTeachingGroupFromTimetableEntry", actions);
        Assert.DoesNotMatch(new Regex(@"while\s*\(\s*true\s*\)|autoRetry|forceOverwrite", RegexOptions.IgnoreCase), actions);
    }

    [Fact]
    public void Forbidden_assign_clear_do_not_auto_create_TeachingGroup()
    {
        var assign = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs");
        Assert.DoesNotContain("new TeachingGroup", assign);
        Assert.DoesNotContain("AddAsync", assign);
        Assert.Contains("EnsureCompatibleWithTimetableEntry", assign);
        Assert.Contains("AssignToTimetableEntryAsync", assign);
        Assert.Contains("ClearFromTimetableEntryAsync", assign);
        Assert.Contains("ITimetableSectionProjector", assign);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntryAsync", assign);
        Assert.Contains("ClearTimetableEntryProjectionAsync", assign);
        Assert.DoesNotContain("new TimetableSection", assign);
    }

    private static string ExtractType(string src, string typeName)
    {
        var marker = "class " + typeName;
        var start = src.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            marker = "record " + typeName;
            start = src.IndexOf(marker, StringComparison.Ordinal);
        }

        Assert.True(start >= 0, typeName);
        var brace = src.IndexOf('{', start);
        var end = src.IndexOf('}', brace);
        return src.Substring(brace, end - brace + 1);
    }

    private static string ExtractMethod(string src, string methodName)
    {
        var marker = methodName + "(";
        var start = src.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, methodName);
        var brace = src.IndexOf('{', start);
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

        return src[start..];
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(parts).ToArray()));

    private static string Rel(string fullPath)
        => Path.GetRelativePath(FindRepoRoot(), fullPath).Replace('\\', '/');

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
