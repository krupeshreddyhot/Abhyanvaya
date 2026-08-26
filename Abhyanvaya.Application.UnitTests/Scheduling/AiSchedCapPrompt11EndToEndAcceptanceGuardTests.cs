using System.Text.RegularExpressions;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 11 — End-to-end scheduling acceptance guards.
/// Locks scenario evidence across CAP 1–10 + TG.4A–TG.6 without redesigning architecture.
/// </summary>
public sealed class AiSchedCapPrompt11EndToEndAcceptanceGuardTests
{
    [Fact]
    public void Scenario1_Create_draft_entry_omits_TeachingGroup_and_forbids_inference()
    {
        var dtos = ReadApp("DTOs", "Scheduling", "TimetableDtos.cs");
        Assert.DoesNotContain("TeachingGroupId", ExtractTypeBody(dtos, "CreateTimetableEntryRequest"));
        Assert.DoesNotContain("TeachingGroupId", ExtractTypeBody(dtos, "UpdateTimetableEntryRequest"));
        Assert.DoesNotContain("TeachingGroupId", ExtractTypeBody(dtos, "UpsertTimetableEntryRequest"));

        var contract = ReadUi("timetable", "timetableTeachingGroupSelectorContract.ts");
        Assert.Contains("shouldInferTeachingGroupFromSubjectAllocation", contract);
        Assert.Contains("false", contract);

        var dialog = ReadUi("timetable", "TimetableEntryDialog.tsx");
        Assert.Contains("Create without TeachingGroupId", dialog);
        Assert.DoesNotContain("createTeachingGroup(", dialog);
    }

    [Fact]
    public void Scenario2_Assign_projects_from_SoT_via_projector_only()
    {
        var assign = ReadApp("Scheduling", "TeachingGroupApplicationService.cs");
        Assert.Contains("entry.TeachingGroupId = teachingGroup.Id", assign);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntryAsync", assign);
        Assert.DoesNotContain("new TimetableSection", assign);

        AssertSoleProjectorWriter();
        Assert.DoesNotContain("setTimetableSections", ReadUi("timetable", "TimetableEntryDialog.tsx"));
        Assert.DoesNotContain("new TimetableSection", ReadUiRoot("services", "schedulingService.ts"));
    }

    [Fact]
    public void Scenario3_Clear_nulls_TeachingGroupId_and_clears_projection()
    {
        var assign = ReadApp("Scheduling", "TeachingGroupApplicationService.cs");
        var clearStart = assign.IndexOf("ClearFromTimetableEntryAsync", StringComparison.Ordinal);
        var clearEnd = assign.IndexOf("LoadTeachingGroupForAssignmentAsync", clearStart, StringComparison.Ordinal);
        var body = assign[clearStart..clearEnd];
        Assert.Contains("ClearTimetableEntryProjectionAsync", body);
        Assert.Contains("entry.TeachingGroupId = null", body);
        Assert.True(
            body.IndexOf("ClearTimetableEntryProjectionAsync", StringComparison.Ordinal)
            < body.IndexOf("entry.TeachingGroupId = null", StringComparison.Ordinal));
    }

    [Fact]
    public void Scenario4_Membership_resolution_is_server_authoritative()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TeachingGroupMembershipResolver.cs")));

        var membershipUi = ReadUi("teachingGroupMembershipUi.ts");
        Assert.Contains("shouldCalculateResolvedMembershipInUi = (): boolean => false", membershipUi);

        // No second client-side membership resolution engine.
        Assert.DoesNotContain("function resolveMembership", membershipUi);
        Assert.DoesNotContain("computeResolvedRoster", membershipUi);
        Assert.DoesNotContain("base.union", membershipUi);
    }

    [Fact]
    public void Scenario5_TeachingGroup_capacity_blocks_publish_not_draft()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "Rules", "TeachingGroupCapacityExceededRule.cs")));

        var readiness = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.Contains("TEACHING_GROUP_CAPACITY_EXCEEDED", readiness);
        Assert.Contains("IsBlockingConflict", readiness);

        // Draft soft path never blocks editing (frozen ConflictSummary contract).
        var conflictModels = ReadApp("Scheduling", "Conflicts", "ConflictModels.cs");
        Assert.Contains("BlocksEditing => false", conflictModels);

        var designer = ReadUi("timetable", "TimetableDesignerPage.tsx");
        Assert.DoesNotContain("TEACHING_GROUP_CAPACITY_EXCEEDED", designer);
        Assert.DoesNotContain("MaxTeachingCapacity", designer);
    }

    [Fact]
    public void Scenario6_Room_capacity_uses_shared_PlacementSize_and_RoomCapacityEvaluator()
    {
        var di = ReadApp("DependencyInjection.cs");
        Assert.Equal(1, Regex.Matches(di, @"IPlacementSizeResolver").Count);
        Assert.Equal(1, Regex.Matches(di, @"IRoomCapacityEvaluator").Count);

        var readiness = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.Contains("ROOM_CAPACITY", readiness);

        var panel = ReadUi("PublishReadinessPanel.tsx");
        Assert.DoesNotContain("PlacementSizeResolver", panel);
        Assert.DoesNotContain("RoomCapacityEvaluator", panel);
        Assert.DoesNotContain("effectiveRoomCapacity", panel);
    }

    [Fact]
    public void Scenario7_ConflictEngine_detects_and_draft_does_not_block_editing()
    {
        var reg = ReadApp("Scheduling", "Conflicts", "ConflictRuleRegistration.cs");
        Assert.Equal(1, Regex.Matches(reg, @"AddScoped<\s*ConflictEngine").Count);

        var models = ReadApp("Scheduling", "Conflicts", "ConflictModels.cs");
        Assert.Contains("BlocksEditing => false", models);

        var readiness = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.Contains("IsBlockingConflict", readiness);
        Assert.Contains("Critical", readiness);
    }

    [Fact]
    public void Scenario8_Publish_readiness_GET_is_deterministic_and_read_only()
    {
        var service = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.Contains("OrderDeterministically", service);
        Assert.Contains("IsBlocking", service);
        Assert.DoesNotContain("SaveChangesAsync", service);
        Assert.DoesNotContain("IUnitOfWork", service);

        var controller = ReadApi("Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("publish-readiness", controller);
        Assert.Contains("EvaluatePublishReadinessAsync", controller);
        Assert.Contains("CanViewSchedulingTimetable", controller);
    }

    [Fact]
    public void Scenario9_Publish_blocked_throws_PublishNotReadyException_before_mutation()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        var gate = lifecycle.IndexOf("EvaluatePublishReadinessAsync", StringComparison.Ordinal);
        var mutate = lifecycle.IndexOf("entity.Status = TimetableStatus.Published", StringComparison.Ordinal);
        var save = lifecycle.IndexOf("ConcurrencyExceptionHelper.SaveChangesAsync", StringComparison.Ordinal);
        Assert.True(gate > 0 && mutate > gate && save > mutate);
        Assert.Contains("PublishNotReadyException", lifecycle);

        var controller = ReadApi("Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("PublishNotReadyException", controller);
        Assert.Contains("ex.Readiness", controller);
    }

    [Fact]
    public void Scenario10_Publish_ready_uses_same_readiness_service_once()
    {
        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.Equal(1, Regex.Matches(lifecycle, @"EvaluatePublishReadinessAsync").Count);
        Assert.Contains("ITimetablePublishReadinessService", lifecycle);
        Assert.DoesNotContain("PlacementSize", lifecycle);
        Assert.DoesNotContain("ConflictEngine", lifecycle);
    }

    [Fact]
    public void Scenario11_Archived_assigned_TG_is_represented_not_cleared()
    {
        var compatible = ReadApp("Scheduling", "CompatibleTeachingGroupQueryService.cs");
        Assert.Contains("Archived", compatible);
        Assert.DoesNotContain("TeachingGroupId = null", compatible);
        Assert.DoesNotContain("ClearFromTimetableEntry", compatible);
        Assert.DoesNotContain("SaveChanges", compatible);

        var uiLabel = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "abhyanvaya-ui", "src", "pages", "setup", "scheduling", "timetable", "timetableUtils.ts"));
        Assert.Contains("Archived", uiLabel);
    }

    [Fact]
    public void Scenario12_Legacy_null_TeachingGroupId_remains_valid_without_inference()
    {
        var placement = ReadApp("Scheduling", "Capacity", "PlacementSizeResolver.cs");
        Assert.DoesNotContain("CreateTeachingGroup", placement);
        Assert.DoesNotContain("AssignToTimetableEntry", placement);
        Assert.DoesNotContain("InferTeachingGroup", placement);

        // PlacementSizeResolver resolves size without inventing Teaching Groups.
        Assert.Contains("PlacementSize", placement);

        var contract = ReadUi("timetable", "timetableTeachingGroupSelectorContract.ts");
        Assert.Contains("shouldInferTeachingGroupFromSubjectAllocation", contract);
        Assert.Contains("false", contract);
        Assert.DoesNotMatch(
            new Regex(@"shouldInferTeachingGroupFromSubjectAllocation[^\n]{0,80}true"),
            contract);
    }

    [Fact]
    public void Scenario13_Concurrency_maps_to_established_conflict_without_auto_retry()
    {
        var helper = ReadApp("Internal", "ConcurrencyExceptionHelper.cs");
        Assert.Contains("ForSchedulingModule", helper);
        Assert.Contains("TimetableEntry", helper);

        var mapper = ReadApp("Internal", "TeachingGroupMembershipPersistenceExceptionMapper.cs");
        Assert.Contains("ConcurrencyConflictException", mapper);

        var assignActions = ReadUi("timetable", "timetableTeachingGroupAssignmentActions.ts");
        Assert.DoesNotContain("retryAssign", assignActions);
        Assert.DoesNotContain("autoRetry", assignActions);
        Assert.Contains("status === 409", assignActions);
        Assert.Contains("kind: \"conflict\"", assignActions);
    }

    [Fact]
    public void Scenario14_Finding_navigation_opens_entry_without_mutation()
    {
        var panel = ReadUi("PublishReadinessPanel.tsx");
        Assert.Contains("onViewEntry", panel);
        Assert.DoesNotContain("publishTimetable", panel);
        Assert.DoesNotContain("assignTeachingGroup", panel);
        Assert.DoesNotContain("SaveChanges", panel);

        var designer = ReadUi("timetable", "TimetableDesignerPage.tsx");
        Assert.Contains("openEntryFromPublishFinding", designer);

        var publishing = ReadUi("governance", "PublishingPage.tsx");
        Assert.Contains("entryId=", publishing);
        Assert.Contains("onViewEntry", publishing);
        var viewIdx = publishing.IndexOf("onViewEntry", StringComparison.Ordinal);
        var viewSlice = publishing.Substring(viewIdx, Math.Min(350, publishing.Length - viewIdx));
        Assert.Contains("navigate(", viewSlice);
        Assert.DoesNotContain("assignTeachingGroupToTimetableEntry", viewSlice);
        Assert.DoesNotContain("clearTeachingGroupFromTimetableEntry", viewSlice);
    }

    [Fact]
    public void Scenario15_DnD_copy_paste_create_payloads_omit_TeachingGroupId()
    {
        var designer = ReadUi("timetable", "TimetableDesignerPage.tsx");
        Assert.DoesNotMatch(new Regex(@"createTimetableEntry\([^)]*teachingGroupId", RegexOptions.Singleline), designer);
        Assert.DoesNotMatch(new Regex(@"bulkTimetableEntries\([\s\S]*teachingGroupId"), designer);

        var dtos = ReadApp("DTOs", "Scheduling", "TimetableDtos.cs");
        Assert.DoesNotContain("TeachingGroupId", ExtractTypeBody(dtos, "CreateTimetableEntryRequest"));
        Assert.DoesNotContain("TeachingGroupId", ExtractTypeBody(dtos, "UpsertTimetableEntryRequest"));
    }

    [Fact]
    public void Architecture_checks_hold()
    {
        AssertSoleProjectorWriter();

        var projector = ReadApp("Scheduling", "TimetableSectionProjector.cs");
        Assert.DoesNotContain("SaveChangesAsync", projector);
        Assert.DoesNotContain("IUnitOfWork", projector);

        var schedulingApp = Path.Combine(FindRepoRoot(), "Abhyanvaya.Application", "Scheduling");
        var ignoreHits = Directory.EnumerateFiles(schedulingApp, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("IgnoreQueryFilters", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .ToList();
        Assert.Empty(ignoreHits);

        var readiness = ReadApp("Scheduling", "TimetablePublishReadinessService.cs");
        Assert.DoesNotContain("StudentSection", readiness);
        Assert.DoesNotContain("Attendance", readiness);

        var lifecycle = ReadApp("Scheduling", "TimetableLifecycleService.cs");
        Assert.DoesNotContain("StudentSection", lifecycle);
        Assert.DoesNotContain("new TeachingGroup", lifecycle);
    }

    [Fact]
    public void Acceptance_documentation_exists_with_matrix_and_browser_status()
    {
        var path = Path.Combine(FindRepoRoot(), "docs", "AI_SCHED_CAP_PROMPT_11_END_TO_END_ACCEPTANCE.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("Final recommendation: PASS", text);
        Assert.Contains("BROWSER E2E", text);
        Assert.Contains("NOT EXECUTED", text);
        Assert.Contains("ENVIRONMENT/DATA UNAVAILABLE", text);
        Assert.Contains("Scenario 1", text);
        Assert.Contains("Scenario 15", text);
        Assert.Contains("STOP", text);
        Assert.DoesNotContain("architecture redesigned", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dto_contracts_remain_authoritative_for_readiness()
    {
        Assert.True(typeof(TimetablePublishReadinessResultDto).GetProperty(nameof(TimetablePublishReadinessResultDto.IsReady)) is not null);
        Assert.True(typeof(PublishReadinessFindingDto).GetProperty(nameof(PublishReadinessFindingDto.IsBlocking)) is not null);
        Assert.Contains(Enum.GetNames<TimetableStatus>(), n => n == "Published");
        Assert.Contains(Enum.GetNames<TimetableStatus>(), n => n == "Draft");
    }

    private static void AssertSoleProjectorWriter()
    {
        var writers = Directory.EnumerateFiles(
                Path.Combine(FindRepoRoot(), "Abhyanvaya.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"new\s+TimetableSection\b(?!Dto)"))
            .Select(f => Path.GetRelativePath(FindRepoRoot(), f).Replace('\\', '/'))
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(new[] { "Abhyanvaya.Application/Scheduling/TimetableSectionProjector.cs" }, writers);
    }

    private static string ExtractTypeBody(string source, string typeName)
    {
        var marker = $"class {typeName}";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start > 0, $"Type {typeName} not found");
        var brace = source.IndexOf('{', start);
        var depth = 0;
        for (var i = brace; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[brace..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Unbalanced braces for {typeName}");
    }

    private static string ReadApp(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot(), "Abhyanvaya.Application" }.Concat(parts).ToArray()));

    private static string ReadApi(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot(), "Abhyanvaya.API" }.Concat(parts).ToArray()));

    private static string ReadUi(params string[] parts)
        => File.ReadAllText(Path.Combine(
            new[] { FindRepoRoot(), "abhyanvaya-ui", "src", "pages", "setup", "scheduling" }.Concat(parts).ToArray()));

    private static string ReadUiRoot(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot(), "abhyanvaya-ui", "src" }.Concat(parts).ToArray()));

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
