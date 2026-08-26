using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 3 — Architecture guards (Guards 1–11).
/// </summary>
public sealed class AiSchedCapPrompt3ArchitectureGuardTests
{
    [Fact]
    public void Guard1_single_authoritative_PlacementSize_resolver()
    {
        var path = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling", "Capacity", "PlacementSizeResolver.cs");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("interface IPlacementSizeResolver", text);
        Assert.Contains("class PlacementSizeResolver", text);
        Assert.Contains("ResolvedStudentCount", text);
        Assert.Contains("ExpectedStudentCount", text);
        Assert.Contains("SubjectExpectedCapacity", text);

        var di = File.ReadAllText(Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "DependencyInjection.cs"));
        Assert.Contains("IPlacementSizeResolver", di);
        Assert.Contains("PlacementSizeResolver", di);
    }

    [Fact]
    public void Guard2_ROOM_CAPACITY_uses_PlacementSize()
    {
        var roomRules = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "RoomConflictRules.cs"));
        Assert.Contains("ResolvePlacementSize", roomRules);
        Assert.Contains("RoomCapacityEvaluator.Evaluate", roomRules);
        Assert.DoesNotContain("subject.ExpectedCapacity.HasValue", roomRules);

        var soft = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSoftValidationService.cs"));
        Assert.Contains("_placementSizeResolver.Resolve", soft);
        Assert.Contains("_roomCapacityEvaluator.Evaluate", soft);
        Assert.Contains("ROOM_CAPACITY", soft);
    }

    [Fact]
    public void Guard3_TEACHING_GROUP_CAPACITY_EXCEEDED_uses_TG_capacity()
    {
        var rule = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "TeachingGroupCapacityExceededRule.cs"));
        Assert.Contains("TEACHING_GROUP_CAPACITY_EXCEEDED", rule);
        Assert.Contains("MaxTeachingCapacity", rule);
        Assert.Contains("ResolvedStudentCountsByTeachingGroupId", rule);
        Assert.DoesNotContain("room.Capacity", rule);
        Assert.DoesNotContain("Rooms.TryGetValue", rule);

        var reg = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictRuleRegistration.cs"));
        Assert.Contains("TeachingGroupCapacityExceededRule", reg);
    }

    [Fact]
    public void Guard4_Room_and_TG_capacity_remain_separate()
    {
        var roomRules = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "RoomConflictRules.cs"));
        Assert.DoesNotContain("MaxTeachingCapacity", roomRules);

        var tgRule = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "TeachingGroupCapacityExceededRule.cs"));
        Assert.DoesNotContain("room.Capacity", tgRule);
        Assert.DoesNotContain("Rooms.TryGetValue", tgRule);
        Assert.DoesNotContain("ResolvePlacementSize", tgRule);
    }

    [Fact]
    public void Guard5_and_6_no_SA_to_TG_inference_or_auto_create()
    {
        foreach (var relative in new[]
                 {
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictAnalyzer.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableSoftValidationService.cs"),
                     Path.Combine("Abhyanvaya.Application", "Scheduling", "Capacity", "PlacementSizeResolver.cs"),
                 })
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.DoesNotContain("CreateTeachingGroupIfMissing", src);
            Assert.DoesNotContain("FindFirstTeachingGroup", src);
            Assert.DoesNotContain("InferTeachingGroup", src);
            Assert.DoesNotContain("new TeachingGroup", src);
        }
    }

    [Fact]
    public void Guard7_and_8_draft_remains_soft_no_publish_gate()
    {
        var lifecycle = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableLifecycleService.cs"));
        Assert.DoesNotContain("TEACHING_GROUP_CAPACITY_EXCEEDED", lifecycle);
        Assert.DoesNotContain("PlacementSize", lifecycle);
        Assert.DoesNotContain("publish-readiness", lifecycle, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PublishGate", lifecycle);

        var timetableService = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableService.cs"));
        Assert.DoesNotContain("TEACHING_GROUP_CAPACITY_EXCEEDED", timetableService);
        Assert.DoesNotContain("throw new DomainException(\"ROOM_CAPACITY", timetableService);
    }

    [Fact]
    public void Guard9_no_TimetableSection_direct_writes_outside_projector()
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
    public void Guard10_no_Attendance_changes_in_CAP_Prompt3_files()
    {
        var capFiles = new[]
        {
            Path.Combine("Abhyanvaya.Application", "Scheduling", "Capacity", "PlacementSizeResolver.cs"),
            Path.Combine("Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "TeachingGroupCapacityExceededRule.cs"),
            Path.Combine("Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "RoomConflictRules.cs"),
            Path.Combine("Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictAnalyzer.cs"),
            Path.Combine("Abhyanvaya.Application", "Scheduling", "TimetableSoftValidationService.cs"),
        };
        foreach (var relative in capFiles)
        {
            var src = File.ReadAllText(Path.Combine(FindRepoRoot(), relative));
            Assert.DoesNotContain("StudentSection", src);
            Assert.DoesNotContain("AttendanceSession", src);
            Assert.DoesNotContain("IAttendance", src);
        }
    }

    [Fact]
    public void Guard11_no_TeachingGroup_architecture_model_change()
    {
        var entity = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Domain", "Entities", "Scheduling", "TeachingGroup.cs"));
        Assert.Contains("ExpectedStudentCount", entity);
        Assert.Contains("MaxTeachingCapacity", entity);
        Assert.Contains("TeachingGroupSection", entity);

        var analyzer = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictAnalyzer.cs"));
        Assert.Contains("g.TenantId == tenantId", analyzer);
        Assert.DoesNotContain("IgnoreQueryFilters", analyzer);
    }

    [Fact]
    public void Prompt3_documentation_exists()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_3_PLACEMENT_SIZE_AND_CAPACITY_VALIDATION.md")));
    }

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
