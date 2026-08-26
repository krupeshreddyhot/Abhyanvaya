using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Rules;

public sealed class RoomDoubleBookingRule : IConflictRule
{
    public string RuleCode => "ROOM_DOUBLE_BOOKING";
    public string RuleName => "Double Booked Rooms";
    public ConflictCategory Category => ConflictCategory.Room;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var group in context.Entries.GroupBy(e => new { e.RoomId, e.DayOfWeek, e.TimeSlotId }).Where(g => g.Count() > 1))
        {
            var list = group.ToList();
            foreach (var entry in list)
            {
                var other = list.First(x => x.Id != entry.Id);
                var roomName = context.Rooms.TryGetValue(entry.RoomId, out var r) ? r.Name : $"Room {entry.RoomId}";
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Critical,
                    $"{roomName} is double-booked in the same day and time slot.",
                    $"Entries {entry.Id} and {other.Id} share room {entry.RoomId}.",
                    "Move one class to another room or period.",
                    entry,
                    other.Id));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class RoomCapacityExceededRule : IConflictRule
{
    public string RuleCode => "ROOM_CAPACITY";
    public string RuleName => "Capacity Exceeded";
    public ConflictCategory Category => ConflictCategory.Room;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (!context.Rooms.TryGetValue(entry.RoomId, out var room)) continue;

            var placement = context.ResolvePlacementSize(entry);
            var evaluation = context.RoomCapacityEvaluator.Evaluate(
                room.Capacity,
                context.Thresholds.RoomCapacityMarginPercent,
                placement);
            if (!evaluation.IsExceeded) continue;

            var (description, why, action) = SchedulingConflictPresentationComposer.Instance.RoomCapacityCopy(evaluation);
            bag.Add(context.Create(
                this,
                ConflictSeverity.Error,
                description,
                why,
                action,
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class RoomWrongFeatureRule : IConflictRule
{
    public string RuleCode => "ROOM_WRONG_FEATURE";
    public string RuleName => "Wrong Room Feature";
    public ConflictCategory Category => ConflictCategory.Room;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (!context.Allocations.TryGetValue(entry.SubjectAllocationId, out var allocation)) continue;
            if (!allocation.PreferredRoomId.HasValue) continue;
            if (allocation.PreferredRoomId.Value == entry.RoomId) continue;

            // Prefer-room mismatch treated as feature/type preference guidance
            bag.Add(context.Create(
                this,
                ConflictSeverity.Warning,
                "Assigned room differs from allocation preferred room (feature/location preference).",
                $"Preferred room {allocation.PreferredRoomId}, assigned {entry.RoomId}.",
                "Prefer the allocation's preferred room when free.",
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class RoomWrongTypeRule : IConflictRule
{
    public string RuleCode => "ROOM_WRONG_TYPE";
    public string RuleName => "Wrong Room Type";
    public ConflictCategory Category => ConflictCategory.Room;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (!context.Rooms.TryGetValue(entry.RoomId, out var room)) continue;
            if (!context.Subjects.TryGetValue(entry.SubjectId, out var subject) || !subject.DeliveryTypeId.HasValue) continue;
            if (!context.DeliveryTypes.TryGetValue(subject.DeliveryTypeId.Value, out var delivery)) continue;

            var code = delivery.Code?.ToUpperInvariant() ?? "";
            var needsLab = code.Contains("LAB") || code.Contains("PRACT");
            if (!needsLab) continue;
            if (ConflictAnalysisContext.LabRoomTypes.Contains(room.RoomType)) continue;

            bag.Add(context.Create(
                this,
                ConflictSeverity.Warning,
                $"Delivery type '{delivery.Name}' typically requires a lab room, but '{room.RoomType}' was assigned.",
                "Room type does not match subject delivery expectations.",
                "Assign a lab room type for practical/lab subjects.",
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class RoomUnavailableRule : IConflictRule
{
    public string RuleCode => "ROOM_UNAVAILABLE";
    public string RuleName => "Room Unavailable";
    public ConflictCategory Category => ConflictCategory.Room;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (context.Rooms.TryGetValue(entry.RoomId, out var room) && room.Status != RoomStatus.Available)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Error,
                    $"Room status is {room.Status} (not Available).",
                    "Room master status indicates it should not be scheduled.",
                    "Choose an available room or update room status after maintenance.",
                    entry));
            }

            var blocked = context.RoomAvailabilities.Any(a =>
                a.RoomId == entry.RoomId &&
                (a.AvailabilityType == RoomAvailabilityType.Blocked || a.AvailabilityType == RoomAvailabilityType.Reserved));
            if (blocked)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Error,
                    "Room has blocked/reserved availability in this academic year.",
                    "Room availability records mark the room as not freely bookable.",
                    "Pick another room or clear the availability block.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class RoomMaintenanceConflictRule : IConflictRule
{
    public string RuleCode => "ROOM_MAINTENANCE";
    public string RuleName => "Maintenance Conflict";
    public ConflictCategory Category => ConflictCategory.Room;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            var maintenance = context.RoomAvailabilities.Any(a =>
                a.RoomId == entry.RoomId && a.AvailabilityType == RoomAvailabilityType.Maintenance)
                || (context.Rooms.TryGetValue(entry.RoomId, out var room) && room.Status == RoomStatus.Maintenance);
            if (!maintenance) continue;

            bag.Add(context.Create(
                this,
                ConflictSeverity.Critical,
                "Room is under maintenance for this academic year.",
                "Maintenance availability or room status conflicts with the scheduled entry.",
                "Relocate the class until maintenance completes.",
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class RoomLabRequirementRule : IConflictRule
{
    public string RuleCode => "ROOM_LAB_REQUIRED";
    public string RuleName => "Lab Requirement";
    public ConflictCategory Category => ConflictCategory.Room;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (!context.Allocations.TryGetValue(entry.SubjectAllocationId, out var allocation) || !allocation.LabRequired)
                continue;
            if (!context.Rooms.TryGetValue(entry.RoomId, out var room)) continue;
            if (ConflictAnalysisContext.LabRoomTypes.Contains(room.RoomType)) continue;

            bag.Add(context.Create(
                this,
                ConflictSeverity.Error,
                "Subject allocation requires a lab, but the assigned room is not a lab type.",
                $"Room type is {room.RoomType}.",
                "Assign ComputerLab, ScienceLab, or CommerceLab.",
                entry));
        }
        return Task.CompletedTask;
    }
}
