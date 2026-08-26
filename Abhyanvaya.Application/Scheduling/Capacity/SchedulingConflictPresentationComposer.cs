using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Capacity;

/// <summary>
/// AI-SCHED-CAP Prompt 4 — Presentation classification for scheduling conflict/soft findings.
/// Does not re-evaluate PlacementSize or room capacity; consumes evaluator outputs.
/// </summary>
public interface ISchedulingConflictPresentationComposer
{
    SoftWarningDto CreateRoomCapacitySoftWarning(
        TimetableEntry entry,
        RoomCapacityEvaluation evaluation,
        IReadOnlyList<TimetableWarningDismissal> dismissals);

    SoftWarningDto CreateTeachingGroupCapacitySoftWarning(
        TimetableEntry entry,
        TeachingGroup teachingGroup,
        int resolvedStudentCount,
        int maxTeachingCapacity,
        IReadOnlyList<TimetableWarningDismissal> dismissals);

    SoftWarningDto CreateGenericSoftWarning(
        string code,
        string message,
        TimetableEntry entry,
        IReadOnlyList<TimetableWarningDismissal> dismissals,
        ConflictSeverity presentationSeverity = ConflictSeverity.Warning);

    IReadOnlyList<SoftWarningDto> OrderDeterministically(IEnumerable<SoftWarningDto> warnings);

    (string Description, string WhyOccurred, string SuggestedAction) RoomCapacityCopy(RoomCapacityEvaluation evaluation);

    (string Description, string WhyOccurred, string SuggestedAction) TeachingGroupCapacityCopy(
        TeachingGroup teachingGroup,
        int resolvedStudentCount,
        int maxTeachingCapacity);
}

/// <summary>Shared presentation/classification for ConflictEngine messaging + SoftValidation DTOs.</summary>
public sealed class SchedulingConflictPresentationComposer : ISchedulingConflictPresentationComposer
{
    public static SchedulingConflictPresentationComposer Instance { get; } = new();

    public const string RoomCapacityCode = "ROOM_CAPACITY";
    public const string TeachingGroupCapacityCode = "TEACHING_GROUP_CAPACITY_EXCEEDED";

    public SoftWarningDto CreateRoomCapacitySoftWarning(
        TimetableEntry entry,
        RoomCapacityEvaluation evaluation,
        IReadOnlyList<TimetableWarningDismissal> dismissals)
    {
        var (description, why, action) = RoomCapacityCopy(evaluation);
        return new SoftWarningDto
        {
            Code = RoomCapacityCode,
            Severity = ToSeverityName(ConflictSeverity.Error),
            Title = "Room capacity exceeded",
            Message = description,
            Why = why,
            SuggestedAction = action,
            EntryId = entry.Id,
            StaffId = entry.StaffId,
            RoomId = entry.RoomId,
            DayOfWeek = entry.DayOfWeek,
            TimeSlotId = entry.TimeSlotId,
            TeachingGroupId = entry.TeachingGroupId,
            PlacementSize = evaluation.Placement.Value,
            PlacementSizeSource = evaluation.Placement.Source.ToString(),
            RoomCapacity = evaluation.RoomCapacity,
            CapacityMarginPercent = evaluation.MarginPercent,
            EffectiveRoomCapacity = evaluation.EffectiveCapacity,
            Dismissed = IsDismissed(dismissals, RoomCapacityCode, entry)
        };
    }

    public SoftWarningDto CreateTeachingGroupCapacitySoftWarning(
        TimetableEntry entry,
        TeachingGroup teachingGroup,
        int resolvedStudentCount,
        int maxTeachingCapacity,
        IReadOnlyList<TimetableWarningDismissal> dismissals)
    {
        var (description, why, action) = TeachingGroupCapacityCopy(teachingGroup, resolvedStudentCount, maxTeachingCapacity);
        var statusLabel = teachingGroup.Status == TeachingGroupStatus.Archived
            ? $"{teachingGroup.Status}"
            : null;

        return new SoftWarningDto
        {
            Code = TeachingGroupCapacityCode,
            Severity = ToSeverityName(ConflictSeverity.Error),
            Title = "Teaching Group capacity exceeded",
            Message = description,
            Why = why,
            SuggestedAction = action,
            EntryId = entry.Id,
            StaffId = entry.StaffId,
            RoomId = entry.RoomId,
            DayOfWeek = entry.DayOfWeek,
            TimeSlotId = entry.TimeSlotId,
            TeachingGroupId = teachingGroup.Id,
            TeachingGroupCode = teachingGroup.Code,
            TeachingGroupName = teachingGroup.Name,
            TeachingGroupStatus = statusLabel ?? teachingGroup.Status.ToString(),
            ResolvedStudentCount = resolvedStudentCount,
            MaxTeachingCapacity = maxTeachingCapacity,
            Dismissed = IsDismissed(dismissals, TeachingGroupCapacityCode, entry)
        };
    }

    public SoftWarningDto CreateGenericSoftWarning(
        string code,
        string message,
        TimetableEntry entry,
        IReadOnlyList<TimetableWarningDismissal> dismissals,
        ConflictSeverity presentationSeverity = ConflictSeverity.Warning) =>
        new()
        {
            Code = code,
            Severity = ToSeverityName(presentationSeverity),
            Title = code.Replace('_', ' '),
            Message = message,
            Why = message,
            SuggestedAction = "Review the timetable entry and related scheduling configuration.",
            EntryId = entry.Id,
            StaffId = entry.StaffId,
            RoomId = entry.RoomId,
            DayOfWeek = entry.DayOfWeek,
            TimeSlotId = entry.TimeSlotId,
            TeachingGroupId = entry.TeachingGroupId,
            Dismissed = IsDismissed(dismissals, code, entry)
        };

    public IReadOnlyList<SoftWarningDto> OrderDeterministically(IEnumerable<SoftWarningDto> warnings) =>
        warnings
            .OrderByDescending(w => SeverityRank(w.Severity))
            .ThenBy(w => w.Code, StringComparer.Ordinal)
            .ThenBy(w => w.EntryId ?? int.MaxValue)
            .ThenBy(w => w.DayOfWeek ?? byte.MaxValue)
            .ThenBy(w => w.TimeSlotId ?? int.MaxValue)
            .ThenBy(w => w.RoomId ?? int.MaxValue)
            .ThenBy(w => w.TeachingGroupId ?? int.MaxValue)
            .ToList();

    public (string Description, string WhyOccurred, string SuggestedAction) RoomCapacityCopy(
        RoomCapacityEvaluation evaluation)
    {
        var description =
            $"Room capacity exceeded. Placement size ({evaluation.Placement.Value} from {evaluation.Placement.Source}) " +
            $"is greater than effective room capacity ({evaluation.EffectiveCapacity:0.#}).";
        var why =
            $"Placement size: {evaluation.Placement.Value}. Room capacity: {evaluation.RoomCapacity}. " +
            $"Capacity margin: {evaluation.MarginPercent}%. Effective room capacity: {evaluation.EffectiveCapacity:0.#}.";
        const string action =
            "Select a larger room or adjust the placement configuration (Teaching Group membership/expected size or subject expected capacity).";
        return (description, why, action);
    }

    public (string Description, string WhyOccurred, string SuggestedAction) TeachingGroupCapacityCopy(
        TeachingGroup teachingGroup,
        int resolvedStudentCount,
        int maxTeachingCapacity)
    {
        var label = string.IsNullOrWhiteSpace(teachingGroup.Code)
            ? teachingGroup.Name
            : $"{teachingGroup.Code} — {teachingGroup.Name}";
        if (teachingGroup.Status == TeachingGroupStatus.Archived)
            label += " · Archived";

        var description =
            $"Teaching Group capacity exceeded. Teaching Group '{label}' has {resolvedStudentCount} resolved students " +
            $"but a maximum teaching capacity of {maxTeachingCapacity}.";
        var why =
            $"Resolved student count: {resolvedStudentCount}. Maximum teaching capacity: {maxTeachingCapacity}. " +
            "This is independent of room seating capacity.";
        const string action =
            "Review Teaching Group membership/capacity or select an appropriate Teaching Group. Do not confuse this with room capacity.";
        return (description, why, action);
    }

    public static string ToSeverityName(ConflictSeverity severity) => severity switch
    {
        ConflictSeverity.Critical => "Critical",
        ConflictSeverity.Error => "Error",
        ConflictSeverity.Warning => "Warning",
        ConflictSeverity.Information => "Information",
        _ => "Warning"
    };

    private static int SeverityRank(string? severity) => severity switch
    {
        "Critical" => 4,
        "Error" => 3,
        "Warning" => 2,
        "Information" => 1,
        _ => 0
    };

    private static bool IsDismissed(
        IReadOnlyList<TimetableWarningDismissal> dismissals,
        string code,
        TimetableEntry entry) =>
        dismissals.Any(d =>
            d.WarningCode == code
            && d.EntryId == entry.Id
            && d.StaffId == entry.StaffId
            && d.RoomId == entry.RoomId
            && d.DayOfWeek == entry.DayOfWeek
            && d.TimeSlotId == entry.TimeSlotId);
}
