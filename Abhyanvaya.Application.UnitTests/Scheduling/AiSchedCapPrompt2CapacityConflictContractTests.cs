namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 2 — Contract/architecture guards (no production behavior change).
/// </summary>
public sealed class AiSchedCapPrompt2CapacityConflictContractTests
{
    [Fact]
    public void Contract_document_locks_PlacementSize_precedence_and_enforcement_levels()
    {
        var text = ReadDoc("AI_SCHED_CAP_PROMPT_2_CAPACITY_AND_CONFLICT_CONTRACT.md");
        Assert.Contains("CONTRACT / ARCHITECTURE DESIGN ONLY", text);
        Assert.Contains("**PASS**", text);
        Assert.Contains("ResolvedStudentCount → ExpectedStudentCount → Subject.ExpectedCapacity", text);
        Assert.Contains("LEVEL 1", text);
        Assert.Contains("LEVEL 2", text);
        Assert.Contains("LEVEL 3", text);
        Assert.Contains("Publish Gate", text);
        Assert.Contains("TEACHING_GROUP_CAPACITY_EXCEEDED", text);
        Assert.Contains("ROOM_CAPACITY", text);
        Assert.Contains("Recommended Prompt 3", text);
        Assert.Contains("no IgnoreQueryFilters", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No Attendance schema changes", text);
        Assert.Contains("Draft construction continues to permit conflicts", text);
    }

    [Fact]
    public void Contract_distinguishes_Room_Capacity_from_MaxTeachingCapacity()
    {
        var text = ReadDoc("AI_SCHED_CAP_PROMPT_2_CAPACITY_AND_CONFLICT_CONTRACT.md");
        Assert.Contains("Room.Capacity ≠ MaxTeachingCapacity", text);
        Assert.Contains("Physical seats", text);
        Assert.Contains("Do NOT merge these concepts", text);
    }

    [Fact]
    public void Contract_preserves_legacy_TG_null_and_forbids_inference()
    {
        var text = ReadDoc("AI_SCHED_CAP_PROMPT_2_CAPACITY_AND_CONFLICT_CONTRACT.md");
        Assert.Contains("TeachingGroupId = null", text);
        Assert.Contains("Auto-create TG", text);
        Assert.Contains("Infer TG from SubjectAllocation", text);
        Assert.Contains("ConflictEngine remains extension surface", text);
        Assert.Contains("SA→TG inference", text);
    }

    [Fact]
    public void Frozen_TG_architecture_and_ConflictEngine_extension_surface_intact()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictEngine.cs")));
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSoftValidationService.cs")));

        var projector = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs"));
        Assert.Contains(": ITimetableSectionProjector", projector);
        Assert.DoesNotContain("SaveChangesAsync", projector);

        var writers = Directory.EnumerateFiles(
                Path.Combine(FindRepoRoot(), "Abhyanvaya.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => System.Text.RegularExpressions.Regex.IsMatch(
                File.ReadAllText(f), @"new\s+TimetableSection\b(?!Dto)"))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);

        var assign = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs"));
        Assert.DoesNotContain("CreateTeachingGroupIfMissing", assign);
        Assert.DoesNotContain("FindFirstTeachingGroup", assign);
        Assert.DoesNotContain(".IgnoreQueryFilters", assign);

        var attendance = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs"));
        Assert.DoesNotContain("new TeachingGroup", attendance);
        Assert.DoesNotContain("SaveChanges", attendance);
    }

    [Fact]
    public void Prompt2_did_not_introduce_publish_gate_or_hard_capacity_rejection()
    {
        // Prompt 2 was contract-only; Prompt 3 owns PlacementSize. Publish gate remains deferred.
        var lifecycle = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableLifecycleService.cs"));
        Assert.DoesNotContain("publish-readiness", lifecycle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PlacementSize", lifecycle);

        var contract = ReadDoc("AI_SCHED_CAP_PROMPT_2_CAPACITY_AND_CONFLICT_CONTRACT.md");
        Assert.Contains("CONTRACT / ARCHITECTURE DESIGN ONLY", contract);
        Assert.Contains("not implemented in Prompt 2", contract);
    }

    private static string ReadDoc(string fileName)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), "docs", fileName));

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
