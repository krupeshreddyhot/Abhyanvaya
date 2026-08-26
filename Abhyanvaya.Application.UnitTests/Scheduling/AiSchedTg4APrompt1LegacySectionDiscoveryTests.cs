namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 1 — Discovery confirmation (source inspection only; no production changes).
/// </summary>
public sealed class AiSchedTg4APrompt1LegacySectionDiscoveryTests
{
    [Fact]
    public void TimetableSectionsController_exposes_GET_and_PUT_with_scheduling_policies()
    {
        var src = Read("Abhyanvaya.API", "Controllers", "SectionsController.cs");
        Assert.Contains("[Route(\"api/timetable/{timetableId:int}/sections\")]", src);
        Assert.Contains("class TimetableSectionsController", src);
        Assert.Contains("CanViewSchedulingTimetable", src);
        Assert.Contains("CanManageSchedulingTimetable", src);
        Assert.Contains("SetTimetableSectionsAsync", src);
        Assert.Contains("GetTimetableSectionsAsync", src);
    }

    [Fact]
    public void SetTimetableSectionsAsync_routes_through_TeachingGroup_boundary_after_Prompt5()
    {
        // Prompt 1 discovered direct TimetableSection writes; Prompt 5 retrofitted the bridge.
        var service = Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs");
        Assert.Contains("SetTimetableSectionsAsync", service);
        Assert.Contains("request.SectionIds", service);

        var method = ExtractMethod(service, "SetTimetableSectionsAsync");
        Assert.Contains("ReplaceSectionsAndProjectAsync", method);
        Assert.Contains("TeachingGroupId", method);
        Assert.DoesNotContain("new TimetableSection", method);
        Assert.DoesNotContain("IgnoreQueryFilters", method);
        Assert.DoesNotContain("CreateTeachingGroup", method);
    }

    [Fact]
    public void Request_DTO_is_legacy_section_ids_only()
    {
        var dto = Read("Abhyanvaya.Application", "DTOs", "Academic", "SectionDtos.cs");
        var block = ExtractClass(dto, "SetTimetableSectionsRequest");
        Assert.Contains("TimetableEntryId", block);
        Assert.Contains("SectionIds", block);
        Assert.DoesNotContain("TeachingGroupId", block);
    }

    [Fact]
    public void AttendanceSessionResolver_reads_TimetableSections_not_TeachingGroup()
    {
        var resolver = Read("Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs");
        Assert.Contains("TimetableSections", resolver);
        Assert.DoesNotContain("TeachingGroupSection", resolver);
        Assert.DoesNotContain("CreateTeachingGroup", resolver);
        Assert.Contains("Mode = \"Legacy\"", resolver);
    }

    [Fact]
    public void Scheduling_timetable_clone_and_version_paths_do_not_write_TimetableSection()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableCloneService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "ScheduleVersionService.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.DoesNotContain("new TimetableSection", src);
            Assert.DoesNotContain("TimetableSections", src);
        }
    }

    [Fact]
    public void Only_TimetableSectionProjector_constructs_TimetableSection()
    {
        // Prompt 1: sole writer was SectionManagementService; Prompt 4 moved construction to projector.
        // Note: Map helpers may construct TimetableSectionDto — exclude those.
        var appRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application");
        var hits = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return System.Text.RegularExpressions.Regex.IsMatch(text, @"new\s+TimetableSection\b(?!Dto)");
            })
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(
            ["Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs"],
            hits);
    }

    [Fact]
    public void Discovery_document_exists()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_4A_PROMPT_1_LEGACY_SECTION_MUTATION_DISCOVERY.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("DISCOVERY ONLY", text);
        Assert.Contains("TeachingGroupSection", text);
        Assert.Contains("SetTimetableSectionsAsync", text);
        Assert.Contains("STATUS: PASS", text);
    }

    private static string ExtractClass(string src, string className)
    {
        var marker = "class " + className;
        var start = src.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Class {className} not found");
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

        return src.Substring(start);
    }

    private static string ExtractMethod(string src, string methodName)
    {
        var start = src.IndexOf(methodName + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method {methodName} not found");
        var attrStart = src.LastIndexOf('[', start);
        if (attrStart < 0 || start - attrStart > 500) attrStart = start;
        // Prefer method signature start
        var sig = src.LastIndexOf("public async Task", start);
        if (sig >= 0 && start - sig < 200) attrStart = sig;
        var brace = src.IndexOf('{', start);
        var depth = 0;
        for (var i = brace; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return src.Substring(attrStart, i - attrStart + 1);
            }
        }

        return src.Substring(attrStart, Math.Min(2000, src.Length - attrStart));
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
