using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 6 — Read-only Publish Readiness orchestration.
/// Composes ConflictAnalyzer (no persistence), lifecycle preconditions mirroring PublishAsync,
/// and presentation/capacity metrics from existing evaluators. Observational only — no unit-of-work commits.
/// </summary>
public sealed class TimetablePublishReadinessService : ITimetablePublishReadinessService
{
    public const string LifecycleFrozenCode = "LIFECYCLE_FROZEN";
    public const string LifecycleNotEligibleCode = "LIFECYCLE_NOT_ELIGIBLE";
    public const string LifecycleArchivedCode = "LIFECYCLE_ARCHIVED";
    public const string LifecyclePublishedScopeConflictCode = "LIFECYCLE_PUBLISHED_SCOPE_CONFLICT";

    private readonly ITimetableRepository _timetableRepository;
    private readonly IScheduleVersionRepository _versionRepository;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IConflictAnalysisRunner _conflictAnalyzer;

    public TimetablePublishReadinessService(
        ITimetableRepository timetableRepository,
        IScheduleVersionRepository versionRepository,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IConflictAnalysisRunner conflictAnalyzer)
    {
        _timetableRepository = timetableRepository;
        _versionRepository = versionRepository;
        _db = db;
        _currentUser = currentUser;
        _conflictAnalyzer = conflictAnalyzer;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<TimetablePublishReadinessResultDto> EvaluatePublishReadinessAsync(
        int timetableId,
        CancellationToken cancellationToken = default)
    {
        var timetable = await _timetableRepository.GetByIdAsync(TenantId, timetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");

        var findings = new List<PublishReadinessFindingDto>();

        // Lifecycle preconditions — mirror TimetableLifecycleService.PublishAsync (read-only).
        findings.AddRange(await EvaluateLifecyclePreconditionsAsync(timetable, cancellationToken));

        // ConflictEngine via IConflictAnalysisRunner only (no detection-run persistence).
        var (context, bag) = await _conflictAnalyzer.AnalyzeAsync(
            TenantId,
            timetable.AcademicYearId,
            timetable.Id,
            timetable.DepartmentId,
            cancellationToken);

        var entriesById = context.Entries.ToDictionary(e => e.Id);
        foreach (var item in bag.Items)
        {
            findings.Add(MapConflictFinding(item, context, entriesById));
        }

        var ordered = OrderDeterministically(findings);
        var blocking = ordered.Count(f => f.IsBlocking);
        var warnings = ordered.Count(f =>
            !f.IsBlocking && string.Equals(f.Severity, "Warning", StringComparison.Ordinal));
        var informational = ordered.Count(f =>
            !f.IsBlocking && string.Equals(f.Severity, "Information", StringComparison.Ordinal));

        return new TimetablePublishReadinessResultDto
        {
            TimetableId = timetable.Id,
            LifecycleState = timetable.Status,
            IsFrozen = timetable.IsFrozen,
            IsReady = blocking == 0,
            BlockingFindingCount = blocking,
            WarningFindingCount = warnings,
            InformationalFindingCount = informational,
            EvaluatedAtUtc = DateTime.UtcNow,
            Findings = ordered
        };
    }

    /// <summary>
    /// Read-only mirror of PublishAsync lifecycle gates. Does not mutate; messages match PublishAsync.
    /// </summary>
    private async Task<IReadOnlyList<PublishReadinessFindingDto>> EvaluateLifecyclePreconditionsAsync(
        Timetable timetable,
        CancellationToken cancellationToken)
    {
        var list = new List<PublishReadinessFindingDto>();

        if (timetable.Status == TimetableStatus.Archived)
        {
            list.Add(LifecycleFinding(
                LifecycleArchivedCode,
                ConflictSeverity.Error,
                "Timetable is archived",
                "Archived timetables cannot be published.",
                "Restore or create a new draft timetable instead of publishing an archived one."));
        }

        if (timetable.IsFrozen)
        {
            list.Add(LifecycleFinding(
                LifecycleFrozenCode,
                ConflictSeverity.Error,
                "Timetable is frozen",
                "Frozen timetables cannot be republished until unlocked.",
                "Unlock the frozen timetable, then re-evaluate publish readiness."));
        }

        var versionApproved = false;
        if (timetable.ScheduleVersionId.HasValue)
        {
            var version = await _versionRepository.GetByIdAsync(
                TenantId, timetable.ScheduleVersionId.Value, cancellationToken);
            versionApproved = version?.Status == ScheduleVersionStatus.Approved;
        }

        if (timetable.Status != TimetableStatus.Locked && !versionApproved
            && timetable.Status != TimetableStatus.Archived)
        {
            // Archived already blocked above; still report eligibility for Draft/Published.
            list.Add(LifecycleFinding(
                LifecycleNotEligibleCode,
                ConflictSeverity.Error,
                "Timetable is not eligible for publish",
                "Timetable must be locked or linked to an approved schedule version to publish.",
                "Lock the timetable or ensure its schedule version is approved before publishing."));
        }

        var scopeConflict = await _db.SchedulingTimetables.AsNoTracking().AnyAsync(x =>
            x.TenantId == TenantId
            && x.Id != timetable.Id
            && x.AcademicYearId == timetable.AcademicYearId
            && x.DepartmentId == timetable.DepartmentId
            && x.Status == TimetableStatus.Published
            && !x.IsFrozen, cancellationToken);

        if (scopeConflict)
        {
            list.Add(LifecycleFinding(
                LifecyclePublishedScopeConflictCode,
                ConflictSeverity.Error,
                "Another published timetable exists",
                "Another published timetable already exists for this academic year and department scope.",
                "Archive or freeze the other published timetable, or adjust department/academic year scope."));
        }

        return list;
    }

    private PublishReadinessFindingDto MapConflictFinding(
        ConflictResult item,
        ConflictAnalysisContext context,
        IReadOnlyDictionary<int, TimetableEntry> entriesById)
    {
        var isBlocking = IsBlockingConflict(item);
        var severityName = SchedulingConflictPresentationComposer.ToSeverityName(item.Severity);

        int? teachingGroupId = null;
        string? tgCode = null;
        string? tgName = null;
        string? tgStatus = null;
        int? placementSize = null;
        string? placementSource = null;
        int? roomCapacity = null;
        decimal? margin = null;
        decimal? effective = null;
        int? resolved = null;
        int? maxCap = null;

        TimetableEntry? entry = null;
        if (item.TimetableEntryId is int entryId)
            entriesById.TryGetValue(entryId, out entry);

        if (entry is not null)
            teachingGroupId = entry.TeachingGroupId;

        if (item.RuleCode == "ROOM_CAPACITY" && entry is not null
            && context.Rooms.TryGetValue(entry.RoomId, out var room))
        {
            var placement = context.ResolvePlacementSize(entry);
            var evaluation = context.RoomCapacityEvaluator.Evaluate(
                room.Capacity,
                context.Thresholds.RoomCapacityMarginPercent,
                placement);
            if (evaluation.IsEvaluable)
            {
                placementSize = evaluation.Placement.Value;
                placementSource = evaluation.Placement.Source.ToString();
                roomCapacity = evaluation.RoomCapacity;
                margin = evaluation.MarginPercent;
                effective = evaluation.EffectiveCapacity;
            }
        }

        if (item.RuleCode == "TEACHING_GROUP_CAPACITY_EXCEEDED"
            && teachingGroupId is int tgId
            && context.TeachingGroups.TryGetValue(tgId, out var tg))
        {
            tgCode = tg.Code;
            tgName = tg.Name;
            tgStatus = tg.Status.ToString();
            maxCap = tg.MaxTeachingCapacity;
            if (context.ResolvedStudentCountsByTeachingGroupId.TryGetValue(tgId, out var count))
                resolved = count;
        }

        return new PublishReadinessFindingDto
        {
            Code = item.RuleCode,
            Severity = severityName,
            IsBlocking = isBlocking,
            Title = item.RuleName,
            Why = item.WhyOccurred,
            RecommendedAction = item.Recommendation.SuggestedResolution,
            TimetableEntryId = item.TimetableEntryId,
            DayOfWeek = item.DayOfWeek,
            TimeSlotId = item.TimeSlotId,
            RoomId = item.RoomId,
            TeachingGroupId = teachingGroupId,
            TeachingGroupCode = tgCode,
            TeachingGroupName = tgName,
            TeachingGroupStatus = tgStatus,
            PlacementSize = placementSize,
            PlacementSizeSource = placementSource,
            RoomCapacity = roomCapacity,
            CapacityMarginPercent = margin,
            EffectiveRoomCapacity = effective,
            ResolvedStudentCount = resolved,
            MaxTeachingCapacity = maxCap
        };
    }

    /// <summary>Prompt 5 Level-3 classification — do not expand.</summary>
    public static bool IsBlockingConflict(ConflictResult item) =>
        item.Severity == ConflictSeverity.Critical
        || item.RuleCode is "ROOM_CAPACITY" or "TEACHING_GROUP_CAPACITY_EXCEEDED";

    private static PublishReadinessFindingDto LifecycleFinding(
        string code,
        ConflictSeverity severity,
        string title,
        string why,
        string action) =>
        new()
        {
            Code = code,
            Severity = SchedulingConflictPresentationComposer.ToSeverityName(severity),
            IsBlocking = true,
            Title = title,
            Why = why,
            RecommendedAction = action
        };

    public static IReadOnlyList<PublishReadinessFindingDto> OrderDeterministically(
        IEnumerable<PublishReadinessFindingDto> findings) =>
        findings
            .OrderByDescending(f => f.IsBlocking)
            .ThenByDescending(f => SeverityRank(f.Severity))
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.TimetableEntryId ?? int.MaxValue)
            .ThenBy(f => f.DayOfWeek ?? byte.MaxValue)
            .ThenBy(f => f.TimeSlotId ?? int.MaxValue)
            .ThenBy(f => f.RoomId ?? int.MaxValue)
            .ThenBy(f => f.TeachingGroupId ?? int.MaxValue)
            .ToList();

    private static int SeverityRank(string? severity) => severity switch
    {
        "Critical" => 4,
        "Error" => 3,
        "Warning" => 2,
        "Information" => 1,
        _ => 0
    };
}
