using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 10 — Architecture / transactional integrity guards.</summary>
public sealed class AiSchedCapPrompt10ArchitectureGuardTests
{
    [Fact]
    public void Assign_order_is_stage_TeachingGroupId_then_project_then_single_SaveChanges()
    {
        var assign = ReadApp("Scheduling", "TeachingGroupApplicationService.cs");
        var methodStart = assign.IndexOf("AssignToTimetableEntryAsync", StringComparison.Ordinal);
        Assert.True(methodStart > 0);
        var methodEnd = assign.IndexOf("ClearFromTimetableEntryAsync", methodStart, StringComparison.Ordinal);
        var body = assign[methodStart..methodEnd];

        var stageIdx = body.IndexOf("entry.TeachingGroupId = teachingGroup.Id", StringComparison.Ordinal);
        var projectIdx = body.IndexOf("SyncTeachingGroupSectionsToTimetableEntryAsync", StringComparison.Ordinal);
        var saveIdx = body.IndexOf("ConcurrencyExceptionHelper.SaveChangesAsync", StringComparison.Ordinal);
        Assert.True(stageIdx > 0 && projectIdx > stageIdx && saveIdx > projectIdx,
            "Assign must stage TeachingGroupId → project → SaveChanges once.");
        Assert.Equal(1, Regex.Matches(body, @"ConcurrencyExceptionHelper\.SaveChangesAsync").Count);
        Assert.DoesNotContain("new TimetableSection", body);
    }

    [Fact]
    public void Clear_order_is_clear_projection_then_null_TeachingGroupId_then_single_SaveChanges()
    {
        var assign = ReadApp("Scheduling", "TeachingGroupApplicationService.cs");
        var methodStart = assign.IndexOf("ClearFromTimetableEntryAsync", StringComparison.Ordinal);
        Assert.True(methodStart > 0);
        var methodEnd = assign.IndexOf("LoadTeachingGroupForAssignmentAsync", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        var body = assign[methodStart..methodEnd];

        var clearIdx = body.IndexOf("ClearTimetableEntryProjectionAsync", StringComparison.Ordinal);
        var nullIdx = body.IndexOf("entry.TeachingGroupId = null", StringComparison.Ordinal);
        var saveIdx = body.IndexOf("ConcurrencyExceptionHelper.SaveChangesAsync", StringComparison.Ordinal);
        Assert.True(clearIdx > 0 && nullIdx > clearIdx && saveIdx > nullIdx,
            "Clear must clear projection → null TeachingGroupId → SaveChanges once.");
        Assert.Equal(1, Regex.Matches(body, @"ConcurrencyExceptionHelper\.SaveChangesAsync").Count);
    }

    [Fact]
    public void Projector_is_sole_TimetableSection_writer_and_never_commits()
    {
        var projector = ReadApp("Scheduling", "TimetableSectionProjector.cs");
        Assert.DoesNotContain("SaveChangesAsync", projector);
        Assert.DoesNotContain("SaveChanges(", projector);
        Assert.DoesNotContain("IUnitOfWork", projector);

        var writers = Directory.EnumerateFiles(
                Path.Combine(FindRepoRoot(), "Abhyanvaya.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"new\s+TimetableSection\b(?!Dto)"))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);
    }

    [Fact]
    public void Publish_gate_blocks_before_mutation_and_readiness_is_read_only()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        var gateIdx = lifecycle.IndexOf("EvaluatePublishReadinessAsync", StringComparison.Ordinal);
        var mutateIdx = lifecycle.IndexOf("entity.Status = TimetableStatus.Published", StringComparison.Ordinal);
        var saveIdx = lifecycle.IndexOf("ConcurrencyExceptionHelper.SaveChangesAsync", StringComparison.Ordinal);
        Assert.True(gateIdx > 0 && mutateIdx > gateIdx && saveIdx > mutateIdx);

        var readiness = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.DoesNotContain("SaveChangesAsync", readiness);
        Assert.DoesNotContain("IUnitOfWork", readiness);
        Assert.DoesNotContain("IgnoreQueryFilters", readiness);
    }

    [Fact]
    public void Scheduling_concurrency_classifier_covers_core_entities()
    {
        var helper = ReadApp("Internal", "ConcurrencyExceptionHelper.cs");
        Assert.Contains("ForSchedulingModule", helper);
        Assert.Contains("TimetableEntry", helper);
        Assert.Contains("TeachingGroup", helper);
        Assert.Contains("TimetableSection", helper);
        Assert.Contains("TeachingGroupMembership", helper);
        Assert.Contains("ClassifyConcurrencyConflict", helper);
    }

    [Fact]
    public void GET_endpoints_do_not_create_infer_or_repair_TG_or_Attendance()
    {
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        Assert.DoesNotContain("CreateTeachingGroup", controller);
        Assert.DoesNotContain("InferTeachingGroup", controller);
        Assert.DoesNotContain("new TeachingGroup", controller);
        Assert.DoesNotContain("StudentSection", controller);

        // publish-readiness GET remains observational
        Assert.Contains("publish-readiness", controller);
        Assert.Contains("EvaluatePublishReadinessAsync", controller);
    }

    [Fact]
    public void Documentation_exists()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "docs", "AI_SCHED_CAP_PROMPT_10_TRANSACTIONAL_INTEGRITY.md")));
    }

    private static string ReadApp(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot(), "Abhyanvaya.Application" }.Concat(parts).ToArray()));

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
