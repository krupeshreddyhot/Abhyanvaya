using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 8 — Architecture Guard & Source-of-Truth Enforcement.
/// Prevents regressions: TimetableSection writes only via approved projector; TeachingGroupSection is SoT.
/// </summary>
public sealed class AiSchedTg4APrompt8ArchitectureGuardTests
{
    private static readonly Regex NewTimetableSectionEntity =
        new(@"new\s+TimetableSection\b(?!Dto)", RegexOptions.Compiled);

    private static readonly string[] ApprovedTimetableSectionWriters =
    [
        "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs",
    ];

    [Fact]
    public void Only_approved_projector_constructs_TimetableSection_entity()
    {
        var appRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application");
        var hits = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => NewTimetableSectionEntity.IsMatch(File.ReadAllText(f)))
            .Select(Rel)
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(ApprovedTimetableSectionWriters, hits);
    }

    [Fact]
    public void Controllers_do_not_construct_or_mutate_TimetableSection_directly()
    {
        var apiRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.API", "Controllers");
        foreach (var file in Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(file);
            Assert.False(NewTimetableSectionEntity.IsMatch(src), Rel(file));
            Assert.DoesNotContain("TimetableSections.Add", src);
            Assert.DoesNotContain("TimetableSections.Remove", src);
            Assert.DoesNotContain("Set<TimetableSection>", src);
        }
    }

    [Fact]
    public void TimetableService_and_clone_version_paths_do_not_write_TimetableSection()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableCloneService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "ScheduleVersionService.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.False(NewTimetableSectionEntity.IsMatch(src), relative);
            Assert.DoesNotContain("TimetableSections", src);
            Assert.DoesNotContain("ITimetableSectionProjector", src);
        }
    }

    [Fact]
    public void Approved_mutation_flow_is_boundary_then_SoT_then_projector()
    {
        var set = ExtractMethod(
            Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs"),
            "SetTimetableSectionsAsync");
        Assert.Contains("ReplaceSectionsAndProjectAsync", set);
        Assert.False(NewTimetableSectionEntity.IsMatch(set));
        Assert.Contains("TeachingGroupId", set);

        var tgSections = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs");
        Assert.Contains("ReplaceSectionsAndProjectAsync", tgSections);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntriesAsync", tgSections);
        Assert.False(NewTimetableSectionEntity.IsMatch(tgSections));

        var projector = Read("Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs");
        Assert.Contains(": ITimetableSectionProjector", projector);
        Assert.True(NewTimetableSectionEntity.IsMatch(projector));
        Assert.DoesNotContain("SaveChangesAsync", projector);
    }

    [Fact]
    public void GET_and_Attendance_do_not_auto_create_or_convert_TeachingGroups()
    {
        var get = ExtractMethod(
            Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs"),
            "GetTimetableSectionsAsync");
        var combined = ExtractMethod(
            Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs"),
            "GetCombinedSessionsAsync");
        var attendance = Read("Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs");

        foreach (var src in new[] { get, combined, attendance })
        {
            Assert.DoesNotContain("new TeachingGroup", src);
            Assert.DoesNotContain("CreateTeachingGroup", src);
            Assert.DoesNotContain("ILegacyTimetableTeachingGroupConversionService", src);
            Assert.DoesNotContain("ConvertAsync", src);
            Assert.DoesNotContain("ReplaceSectionsAndProjectAsync", src);
            Assert.DoesNotContain(".IgnoreQueryFilters", src);
        }
    }

    [Fact]
    public void TeachingGroup_boundaries_forbid_SA_inference_and_IgnoreQueryFilters()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "LegacyTimetableTeachingGroupConversionService.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.DoesNotContain(".IgnoreQueryFilters", src, StringComparison.Ordinal);
            Assert.DoesNotContain("CreateTeachingGroupIfMissing", src);
            Assert.DoesNotContain("FindFirstTeachingGroup", src);
            Assert.DoesNotContain("ResolveTeachingGroupFrom", src);
            Assert.DoesNotContain("SchedulingSubjectAllocations", src);
        }
    }

    [Fact]
    public void Scheduling_code_does_not_mutate_Attendance_or_StudentSection()
    {
        var schedulingRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling");
        foreach (var file in Directory.EnumerateFiles(schedulingRoot, "*.cs", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(file);
            Assert.DoesNotContain("new StudentSection", src, StringComparison.Ordinal);
            Assert.DoesNotContain("StudentSections.Add", src);
            Assert.DoesNotContain("Set<StudentSection>", src);
            // Allow DTO / comments mentioning Attendance; forbid entity writes.
            Assert.DoesNotContain("new Attendance ", src);
            Assert.DoesNotContain("new Attendance{", src);
            Assert.DoesNotContain("Attendances.Add", src);
            Assert.DoesNotContain("Set<Attendance>", src);
            Assert.DoesNotContain("AddAttendances(", src);
        }
    }

    [Fact]
    public void Explicit_TeachingGroup_API_uses_application_boundary_not_DbSet()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        var assign = ExtractMethod(controller, "AssignTeachingGroup");
        var clear = ExtractMethod(controller, "ClearTeachingGroup");
        Assert.Contains("_teachingGroupService.AssignToTimetableEntryAsync", assign);
        Assert.Contains("_teachingGroupService.ClearFromTimetableEntryAsync", clear);
        Assert.DoesNotContain("SchedulingTeachingGroups", assign);
        Assert.DoesNotContain("TimetableSection", assign);
        Assert.DoesNotContain("SetTimetableSections", assign);
    }

    [Fact]
    public void Legacy_sections_API_uses_SectionManagementService_boundary()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "SectionsController.cs");
        Assert.Contains("class TimetableSectionsController", controller);
        Assert.Contains("SetTimetableSectionsAsync", controller);
        Assert.Contains("ISectionManagementService", controller);
        Assert.False(NewTimetableSectionEntity.IsMatch(controller));
        Assert.DoesNotContain("TimetableSections.Add", controller);
        Assert.DoesNotContain("ILegacyTimetableTeachingGroupConversionService", controller);
        Assert.DoesNotContain("SchedulingTeachingGroupSections", controller);
    }

    [Fact]
    public void Conversion_utility_is_not_a_hosted_or_startup_job()
    {
        var di = Read("Abhyanvaya.Application", "DependencyInjection.cs");
        Assert.Contains("AddScoped<ILegacyTimetableTeachingGroupConversionService", di);
        Assert.DoesNotContain("AddHostedService<LegacyTimetableTeachingGroupConversionService>", di);

        var program = Read("Abhyanvaya.API", "Program.cs");
        Assert.DoesNotContain("ILegacyTimetableTeachingGroupConversionService", program);
        Assert.DoesNotContain("LegacyTimetableTeachingGroupConversion", program);
    }

    [Fact]
    public void UI_legacy_sections_client_preserves_API_contract_without_TG_inference()
    {
        var ui = Read("abhyanvaya-ui", "src", "services", "sectionService.ts");
        Assert.Contains("setTimetableSections", ui);
        Assert.Contains("/timetable/${timetableId}/sections", ui);
        Assert.Contains("sectionIds", ui);
        Assert.DoesNotContain("teachingGroupId", ui, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createTeachingGroup", ui, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subjectAllocation", ui, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt8_architecture_guard_document_exists()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_4A_PROMPT_8_ARCHITECTURE_GUARD.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("TeachingGroupSection", text);
        Assert.Contains("TimetableSectionProjector", text);
        Assert.Contains("STATUS: PASS", text);
        Assert.Contains("FORBIDDEN", text);
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
