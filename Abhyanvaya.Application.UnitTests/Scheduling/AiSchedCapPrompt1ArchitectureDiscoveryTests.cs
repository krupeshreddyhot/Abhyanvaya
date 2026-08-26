using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 1 — Discovery guards: no production behavior change; TG freeze intact.
/// </summary>
public sealed class AiSchedCapPrompt1ArchitectureDiscoveryTests
{
    private static readonly Regex NewTimetableSectionEntity =
        new(@"new\s+TimetableSection\b(?!Dto)", RegexOptions.Compiled);

    [Fact]
    public void Discovery_document_exists_and_is_discovery_only()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_CAP_PROMPT_1_SCHEDULING_CAPABILITY_ARCHITECTURE_DISCOVERY.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("DISCOVERY ONLY", text);
        Assert.Contains("**PASS**", text);
        Assert.Contains("ConflictEngine", text);
        Assert.Contains("TeachingGroupSection", text);
        Assert.Contains("TimetableSectionProjector", text);
        Assert.Contains("Recommended Prompt 2", text);
        Assert.DoesNotContain("implement Prompt 2", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Frozen_TG_projection_chain_unchanged_by_this_prompt()
    {
        var projector = Read("Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs");
        Assert.Contains(": ITimetableSectionProjector", projector);
        Assert.DoesNotContain("SaveChangesAsync", projector);

        var appRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application");
        var writers = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => NewTimetableSectionEntity.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);
    }

    [Fact]
    public void Existing_conflict_engine_and_room_capacity_surfaces_still_present()
    {
        // Prompt 1 discovery: ConflictEngine + ROOM_CAPACITY surfaces exist.
        // Prompt 3 superseded Subject.ExpectedCapacity-only evaluation with PlacementSize.
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictEngine.cs")));
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "TimetableSoftValidationService.cs")));
        var roomRules = Read("Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "RoomConflictRules.cs");
        Assert.Contains("ROOM_CAPACITY", roomRules);
        Assert.Contains("ResolvePlacementSize", roomRules);
        var soft = Read("Abhyanvaya.Application", "Scheduling", "TimetableSoftValidationService.cs");
        Assert.Contains("ROOM_CAPACITY", soft);
        Assert.Contains("_placementSizeResolver", soft);
    }

    [Fact]
    public void Attendance_resolver_still_does_not_mutate_TeachingGroup_or_create_TG()
    {
        var resolver = Read("Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs");
        Assert.DoesNotContain("new TeachingGroup", resolver);
        Assert.DoesNotContain("CreateTeachingGroup", resolver);
        Assert.DoesNotContain("SaveChanges", resolver);
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
