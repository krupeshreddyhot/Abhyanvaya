using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 5 — Publish Readiness contract/architecture guards (no production gate).
/// </summary>
public sealed class AiSchedCapPrompt5PublishReadinessContractTests
{
    private static readonly Regex NewTimetableSectionEntity =
        new(@"new\s+TimetableSection\b(?!Dto)", RegexOptions.Compiled);

    [Fact]
    public void Contract_document_locks_publish_readiness_and_level3_blockers()
    {
        var text = ReadDoc();
        Assert.Contains("CONTRACT / ARCHITECTURE DESIGN ONLY", text);
        Assert.Contains("**PASS**", text);
        Assert.Contains("ITimetablePublishReadinessService", text);
        Assert.Contains("EvaluatePublishReadinessAsync", text);
        Assert.Contains("LEVEL 3", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROOM_CAPACITY", text);
        Assert.Contains("TEACHING_GROUP_CAPACITY_EXCEEDED", text);
        Assert.Contains("ConflictSeverity.Critical", text);
        Assert.Contains("canPublish", text);
        Assert.Contains("blockers", text);
        Assert.Contains("deterministic", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Deferred", text);
        Assert.Contains("do not change `PublishAsync`", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Contract_reuses_authoritative_capacity_and_conflict_stack()
    {
        var text = ReadDoc();
        Assert.Contains("IPlacementSizeResolver", text);
        Assert.Contains("IRoomCapacityEvaluator", text);
        Assert.Contains("ConflictEngine", text);
        Assert.Contains("ISchedulingConflictPresentationComposer", text);
        Assert.Contains("ResolvedStudentCount", text);
        Assert.Contains("ExpectedStudentCount", text);
        Assert.Contains("Subject.ExpectedCapacity", text);
        Assert.Contains("no second conflict engine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Contract_defines_lifecycle_legacy_archived_and_ordering()
    {
        var text = ReadDoc();
        Assert.Contains("TeachingGroupId = null", text);
        Assert.Contains("Archived", text);
        Assert.Contains("IsFrozen", text);
        Assert.Contains("Locked", text);
        Assert.Contains("blocking", text);
        Assert.Contains("database row order", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TimetableSectionProjector", text);
    }

    [Fact]
    public void Prompt5_contract_deferred_gate_to_later_prompt_and_did_not_embed_capacity_in_lifecycle()
    {
        // Prompt 5 was contract-only. Prompt 7 owns PublishAsync wiring to ITimetablePublishReadinessService.
        // Lifecycle must not embed capacity rule codes / PlacementSize (gate consumes readiness service).
        var lifecycle = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableLifecycleService.cs"));
        Assert.DoesNotContain("PublishGate", lifecycle);
        Assert.DoesNotContain("publish-readiness", lifecycle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ROOM_CAPACITY", lifecycle);
        Assert.DoesNotContain("TEACHING_GROUP_CAPACITY_EXCEEDED", lifecycle);
        Assert.DoesNotContain("PlacementSize", lifecycle);

        var timetable = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableService.cs"));
        Assert.DoesNotContain("EvaluatePublishReadiness", timetable);

        var doc = ReadDoc();
        Assert.Contains("do not change `PublishAsync`", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Publish_readiness_contract_is_read_only_and_reuses_engines()
    {
        var text = ReadDoc();
        Assert.Contains("Read-only", text);
        Assert.Contains("database mutations", text);
        Assert.Contains("Reuse", text);
        Assert.Contains("SoftValidation", text);
        Assert.Contains("Draft mutations", text);
    }

    [Fact]
    public void Blocker_classification_is_explicit_not_all_warnings()
    {
        var text = ReadDoc();
        Assert.Contains("Do **not** classify every SoftValidation Warning as a publish blocker", text);
        Assert.Contains("regardless of engine severity", text);
        Assert.Contains("Critical scheduling integrity", text);
    }

    [Fact]
    public void Guard_no_TG_inference_create_TimetableSection_Attendance_changes_in_Prompt5()
    {
        // Prompt 5 is docs + tests only — production writers unchanged.
        var appRoot = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application");
        var writers = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => NewTimetableSectionEntity.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);

        var attendance = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs"));
        Assert.DoesNotContain("ITimetablePublishReadinessService", attendance);
        Assert.DoesNotContain("PublishReadiness", attendance);

        var assign = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs"));
        Assert.DoesNotContain("CreateTeachingGroupIfMissing", assign);
        Assert.DoesNotContain("InferTeachingGroup", assign);
    }

    [Fact]
    public void Authoritative_resolvers_remain_registered_and_unchanged_by_gate()
    {
        var di = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "DependencyInjection.cs"));
        Assert.Contains("IPlacementSizeResolver", di);
        Assert.Contains("IRoomCapacityEvaluator", di);
        Assert.Contains("ISchedulingConflictPresentationComposer", di);

        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictEngine.cs")));
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Capacity", "PlacementSizeResolver.cs")));
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Capacity", "RoomCapacityEvaluator.cs")));
    }

    [Fact]
    public void Draft_soft_behavior_and_ConflictEngine_BlocksEditing_unchanged()
    {
        var models = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictModels.cs"));
        Assert.Contains("BlocksEditing => false", models);

        var timetable = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableService.cs"));
        Assert.DoesNotContain("EvaluatePublishReadiness", timetable);
        Assert.DoesNotContain("throw new DomainException(\"ROOM_CAPACITY", timetable);
    }

    [Fact]
    public void Capacity_rules_remain_Error_severity_and_are_documented_as_Level3_blockers()
    {
        var room = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "RoomConflictRules.cs"));
        Assert.Contains("ROOM_CAPACITY", room);
        Assert.Contains("ConflictSeverity.Error", room);

        var tg = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "TeachingGroupCapacityExceededRule.cs"));
        Assert.Contains("TEACHING_GROUP_CAPACITY_EXCEEDED", tg);
        Assert.Contains("ConflictSeverity.Error", tg);

        var text = ReadDoc();
        Assert.Contains("regardless of engine severity", text);
    }

    private static string ReadDoc()
        => File.ReadAllText(Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_5_PUBLISH_READINESS_CONTRACT.md"));

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
