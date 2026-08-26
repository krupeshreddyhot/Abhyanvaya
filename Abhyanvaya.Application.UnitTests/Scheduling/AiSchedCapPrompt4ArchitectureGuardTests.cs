using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 4 — Architecture guards 1–10.</summary>
public sealed class AiSchedCapPrompt4ArchitectureGuardTests
{
    [Fact]
    public void Guard1_PlacementSize_not_recalculated_in_UI()
    {
        var utils = ReadUi("timetable", "timetableUtils.ts");
        Assert.Contains("entryCapacityFeedbackFromSoftWarnings", utils);
        Assert.DoesNotContain("ExpectedCapacity", utils);
        Assert.DoesNotContain("RoomCapacityMarginPercent", utils);

        var grid = ReadUi("timetable", "TimetableGrid.tsx");
        Assert.Contains("entryCapacityFeedbackFromSoftWarnings", grid);
        Assert.DoesNotContain("isResolvedOverMaxTeachingCapacity", grid);
    }

    [Fact]
    public void Guard2_ROOM_CAPACITY_uses_authoritative_evaluator()
    {
        var roomRules = ReadApp("Scheduling", "Conflicts", "Rules", "RoomConflictRules.cs");
        Assert.Contains("RoomCapacityEvaluator.Evaluate", roomRules);
        Assert.Contains("SchedulingConflictPresentationComposer", roomRules);

        var soft = ReadApp("Scheduling", "TimetableSoftValidationService.cs");
        Assert.Contains("_roomCapacityEvaluator.Evaluate", soft);
        Assert.Contains("CreateRoomCapacitySoftWarning", soft);
    }

    [Fact]
    public void Guard3_TG_capacity_remains_separate()
    {
        var composer = ReadApp("Scheduling", "Capacity", "SchedulingConflictPresentationComposer.cs");
        Assert.Contains("TEACHING_GROUP_CAPACITY_EXCEEDED", composer);
        Assert.Contains("independent of room", composer);
        Assert.DoesNotContain("Room.Capacity", composer);
    }

    [Fact]
    public void Guard4_and_5_no_TG_auto_create_or_SA_inference()
    {
        var soft = ReadApp("Scheduling", "TimetableSoftValidationService.cs");
        Assert.DoesNotContain("CreateTeachingGroupIfMissing", soft);
        Assert.DoesNotContain("InferTeachingGroup", soft);
        Assert.DoesNotContain("FindFirstTeachingGroup", soft);

        var composer = ReadApp("Scheduling", "Capacity", "SchedulingConflictPresentationComposer.cs");
        Assert.DoesNotContain("CreateTeachingGroupIfMissing", composer);
        Assert.DoesNotContain("InferTeachingGroup", composer);
        Assert.DoesNotContain("SaveChanges", composer);
    }

    [Fact]
    public void Guard6_no_TimetableSection_writes_outside_projector()
    {
        var writers = Directory.EnumerateFiles(
                Path.Combine(FindRepoRoot(), "Abhyanvaya.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"new\s+TimetableSection\b(?!Dto)"))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);
    }

    [Fact]
    public void Guard7_no_Attendance_or_StudentSection_mutation_in_presentation()
    {
        var composer = ReadApp("Scheduling", "Capacity", "SchedulingConflictPresentationComposer.cs");
        Assert.DoesNotContain("StudentSection", composer);
        Assert.DoesNotContain("AttendanceSession", composer);
        Assert.DoesNotContain("SaveChanges", composer);
    }

    [Fact]
    public void Guard8_and_9_no_publish_gate_or_hard_draft_rejection()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.DoesNotContain("PublishGate", lifecycle);
        Assert.DoesNotContain("ROOM_CAPACITY", lifecycle);

        var timetable = ReadApp("Scheduling", "TimetableService.cs");
        Assert.DoesNotContain("throw new DomainException(\"ROOM_CAPACITY", timetable);
        Assert.DoesNotContain("TEACHING_GROUP_CAPACITY_EXCEEDED", timetable);
    }

    [Fact]
    public void Guard10_no_client_side_compatibility_filtering()
    {
        var contract = ReadUi("timetable", "timetableTeachingGroupSelectorContract.ts");
        Assert.Contains("never filter Teaching Groups", contract);
    }

    [Fact]
    public void Prompt4_documentation_exists()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_4_CONFLICT_PRESENTATION_AND_ACTIONABLE_FEEDBACK.md")));
    }

    private static string ReadApp(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot(), "Abhyanvaya.Application" }.Concat(parts).ToArray()));

    private static string ReadUi(params string[] parts)
        => File.ReadAllText(Path.Combine(new[]
        {
            FindRepoRoot(), "abhyanvaya-ui", "src", "pages", "setup", "scheduling"
        }.Concat(parts).ToArray()));

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
