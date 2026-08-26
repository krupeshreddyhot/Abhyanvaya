using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.4A Prompt 10 — Architectural freeze guardrails (docs + source locks only; no production behavior).
/// </summary>
public sealed class AiSchedTg4APrompt10ArchitecturalFreezeTests
{
    private static readonly Regex NewTimetableSectionEntity =
        new(@"new\s+TimetableSection\b(?!Dto)", RegexOptions.Compiled);

    [Fact]
    public void Freeze_document_exists_and_declares_FULL_PASS_FROZEN()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_TG_4A_FINAL_ACCEPTANCE_AND_FREEZE.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("FULL PASS — FROZEN", text);
        Assert.Contains("TeachingGroupSection", text);
        Assert.Contains("SOURCE OF TRUTH", text);
        Assert.Contains("TimetableSectionProjector", text);
        Assert.Contains("projection-only", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No automatic TeachingGroup creation", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No SubjectAllocation", text);
        Assert.Contains("No Attendance schema", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No UI redesign", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No permanent", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FORBIDDEN", text);
        Assert.Contains("Freeze / documentation / guardrails", text);
    }

    [Fact]
    public void Frozen_SoT_lock_TeachingGroupSection_remains_canonical()
    {
        var iface = Read("Abhyanvaya.Application", "Scheduling", "ITeachingGroupSectionApplicationService.cs");
        Assert.Contains("ReplaceSectionsAndProjectAsync", iface);
        Assert.Contains("ITimetableSectionProjector", iface);

        var sot = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs");
        Assert.DoesNotMatch(NewTimetableSectionEntity, sot);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntriesAsync", sot);
    }

    [Fact]
    public void Frozen_projection_lock_only_projector_writes_TimetableSection()
    {
        var appRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application");
        var hits = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => NewTimetableSectionEntity.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();

        Assert.Equal(["Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs"], hits);

        var projector = Read("Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs");
        Assert.Contains(": ITimetableSectionProjector", projector);
        Assert.DoesNotContain("SaveChangesAsync", projector);
    }

    [Fact]
    public void Frozen_legacy_sections_contract_and_bridge_path()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "SectionsController.cs");
        Assert.Contains("[Route(\"api/timetable/{timetableId:int}/sections\")]", controller);
        Assert.Contains("CanManageSchedulingTimetable", controller);
        Assert.Contains("SetTimetableSectionsRequest", controller);

        var set = ExtractMethod(
            Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs"),
            "SetTimetableSectionsAsync");
        Assert.Contains("ReplaceSectionsAndProjectAsync", set);
        Assert.DoesNotMatch(NewTimetableSectionEntity, set);
        Assert.Contains("TeachingGroupId", set);
        Assert.DoesNotContain("CreateTeachingGroup", set);
    }

    [Fact]
    public void Frozen_no_auto_TG_no_SA_inference_no_permanent_backfill()
    {
        var conversion = Read("Abhyanvaya.Application", "Scheduling", "LegacyTimetableTeachingGroupConversionService.cs");
        Assert.DoesNotContain("new TeachingGroup", conversion);
        Assert.DoesNotContain("SchedulingSubjectAllocations", conversion);
        Assert.Contains("DryRun", conversion);

        var di = Read("Abhyanvaya.Application", "DependencyInjection.cs");
        Assert.Contains("AddScoped<ILegacyTimetableTeachingGroupConversionService", di);
        Assert.DoesNotContain("AddHostedService<LegacyTimetableTeachingGroupConversionService>", di);

        var program = Read("Abhyanvaya.API", "Program.cs");
        Assert.DoesNotContain("ILegacyTimetableTeachingGroupConversionService", program);

        var migrations = Directory.GetFiles(
            Path.Combine(FindRepoRoot(), "Abhyanvaya.Infrastructure", "Persistence", "Migrations"),
            "*TimetableSection*Backfill*",
            SearchOption.AllDirectories);
        Assert.Empty(migrations);
    }

    [Fact]
    public void Frozen_clone_and_version_are_not_TimetableSection_writers()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableCloneService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "ScheduleVersionService.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.DoesNotMatch(NewTimetableSectionEntity, src);
            Assert.DoesNotContain("TimetableSections", src);
            Assert.Contains("CloneEntry", src);
        }

        var timetableService = Read("Abhyanvaya.Application", "Scheduling", "TimetableService.cs");
        Assert.Contains("internal static TimetableEntry CloneEntry(TimetableEntry source, int timetableId)", timetableService);
        Assert.Contains("TeachingGroupId = source.TeachingGroupId", timetableService);
    }

    [Fact]
    public void Frozen_Attendance_and_UI_boundaries_unchanged()
    {
        var attendance = Read("Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs");
        Assert.Contains("TimetableSections.AsNoTracking", attendance);
        Assert.Contains("Mode = \"Legacy\"", attendance);
        Assert.DoesNotContain("new TeachingGroup", attendance);
        Assert.DoesNotContain("ReplaceSectionsAndProjectAsync", attendance);

        var ui = Read("abhyanvaya-ui", "src", "services", "sectionService.ts");
        Assert.Contains("setTimetableSections", ui);
        Assert.Contains("/timetable/${timetableId}/sections", ui);
        Assert.DoesNotContain("createTeachingGroup", ui, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("subjectAllocation", ui, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frozen_future_UI_must_use_application_boundaries_not_DbSet_bypass()
    {
        // API controllers remain the only HTTP surface; no TimetableSection entity construction.
        var apiRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.API", "Controllers");
        foreach (var file in Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(file);
            Assert.DoesNotMatch(NewTimetableSectionEntity, src);
            Assert.DoesNotContain("TimetableSections.Add", src);
        }

        var timetables = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("ITeachingGroupApplicationService", timetables);
        Assert.Contains("AssignToTimetableEntryAsync", timetables);
    }

    private static string ExtractMethod(string src, string methodName)
    {
        var start = src.IndexOf(methodName + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, methodName + " not found");
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

        return src.Substring(start, Math.Min(2500, src.Length - start));
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
