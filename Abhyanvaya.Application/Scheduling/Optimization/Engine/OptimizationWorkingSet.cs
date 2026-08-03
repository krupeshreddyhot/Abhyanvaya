namespace Abhyanvaya.Application.Scheduling.Optimization.Engine;

/// <summary>Pure helpers for in-memory optimization working sets. Never touches production DB rows.</summary>
public static class OptimizationWorkingSet
{
    public static List<OptimizationEntrySnapshot> CloneEntries(IEnumerable<OptimizationEntrySnapshot> source) =>
        source.Select(e => new OptimizationEntrySnapshot
        {
            EntryId = e.EntryId,
            TimetableId = e.TimetableId,
            DayOfWeek = e.DayOfWeek,
            TimeSlotId = e.TimeSlotId,
            StaffId = e.StaffId,
            RoomId = e.RoomId,
            DepartmentId = e.DepartmentId,
            SubjectId = e.SubjectId,
            GroupId = e.GroupId,
            SubjectAllocationId = e.SubjectAllocationId
        }).ToList();

    public static void ApplyCandidate(IList<OptimizationEntrySnapshot> entries, OptimizationCandidate candidate)
    {
        if (!candidate.EntryId.HasValue) return;
        var entry = entries.FirstOrDefault(e => e.EntryId == candidate.EntryId.Value);
        if (entry is null) return;

        if (candidate.ProposedRoomId.HasValue) entry.RoomId = candidate.ProposedRoomId.Value;
        if (candidate.ProposedStaffId.HasValue) entry.StaffId = candidate.ProposedStaffId.Value;
        if (candidate.ProposedTimeSlotId.HasValue) entry.TimeSlotId = candidate.ProposedTimeSlotId.Value;
        if (candidate.ProposedDayOfWeek.HasValue) entry.DayOfWeek = candidate.ProposedDayOfWeek.Value;
    }

    public static void ApplyAll(IList<OptimizationEntrySnapshot> entries, IEnumerable<OptimizationCandidate> candidates)
    {
        foreach (var c in candidates)
            ApplyCandidate(entries, c);
    }

    public static int CountHardConflicts(IReadOnlyList<OptimizationEntrySnapshot> entries)
    {
        var count = 0;
        var byStaffSlot = entries.GroupBy(e => (e.StaffId, e.DayOfWeek, e.TimeSlotId));
        count += byStaffSlot.Sum(g => Math.Max(0, g.Count() - 1));

        var byRoomSlot = entries.GroupBy(e => (e.RoomId, e.DayOfWeek, e.TimeSlotId));
        count += byRoomSlot.Sum(g => Math.Max(0, g.Count() - 1));

        var byGroupSlot = entries.GroupBy(e => (e.GroupId, e.DayOfWeek, e.TimeSlotId));
        count += byGroupSlot.Sum(g => Math.Max(0, g.Count() - 1));

        return count;
    }

    public static Dictionary<string, decimal> BuildMetrics(
        IReadOnlyList<OptimizationEntrySnapshot> entries,
        IReadOnlyDictionary<int, OptimizationRoomSnapshot> rooms,
        IReadOnlyDictionary<int, OptimizationSlotSnapshot> slots,
        IReadOnlyDictionary<int, int> facultyPreferredRooms,
        int conflictCount)
    {
        var entryCount = Math.Max(entries.Count, 1);
        var staffDays = entries.GroupBy(e => (e.StaffId, e.DayOfWeek)).Select(g => g.Count()).ToList();
        var avgDaily = staffDays.Count == 0 ? 0m : (decimal)staffDays.Average();
        var variance = staffDays.Count == 0
            ? 0m
            : (decimal)staffDays.Select(c => Math.Pow((double)(c - avgDaily), 2)).Average();
        var workloadBalance = Math.Clamp(100m - (decimal)Math.Sqrt((double)variance) * 8m, 0m, 100m);

        var preferredHits = 0;
        var preferredEligible = 0;
        foreach (var e in entries)
        {
            if (!facultyPreferredRooms.TryGetValue(e.StaffId, out var preferred)) continue;
            preferredEligible++;
            if (e.RoomId == preferred) preferredHits++;
        }
        var preference = preferredEligible == 0
            ? 55m
            : Math.Round(preferredHits * 100m / preferredEligible, 2);

        var roomLoads = entries.GroupBy(e => e.RoomId).ToDictionary(g => g.Key, g => g.Count());
        var utilScores = new List<decimal>();
        foreach (var kv in roomLoads)
        {
            if (!rooms.TryGetValue(kv.Key, out var room) || room.Capacity <= 0)
            {
                utilScores.Add(60m);
                continue;
            }
            // Capacity utilization proxy: denser assignment toward capacity is better up to 100%.
            var ratio = Math.Min(1m, kv.Value / (decimal)Math.Max(room.Capacity / 10, 1));
            utilScores.Add(Math.Round(ratio * 100m, 2));
        }
        var roomUtil = utilScores.Count == 0 ? 65m : Math.Round(utilScores.Average(), 2);

        var travel = EstimateTravel(entries, rooms);
        var avgBreak = EstimateAverageBreakMinutes(entries, slots);
        var facultyUtil = Math.Clamp(entries.Select(e => e.StaffId).Distinct().Count() == 0
            ? 70m
            : Math.Round(entries.Count * 100m / (entries.Select(e => e.StaffId).Distinct().Count() * 20m), 2), 0m, 100m);

        return new Dictionary<string, decimal>
        {
            ["FacultyUtilization"] = facultyUtil,
            ["RoomUtilization"] = roomUtil,
            ["AverageTravel"] = travel,
            ["AverageBreak"] = avgBreak,
            ["WorkloadBalance"] = workloadBalance,
            ["PreferenceSatisfaction"] = preference,
            ["IdlePeriods"] = Math.Max(0, 10m - avgBreak / 5m),
            ["ConflictDensity"] = Math.Round(conflictCount * 100m / entryCount, 2)
        };
    }

    private static decimal EstimateTravel(
        IReadOnlyList<OptimizationEntrySnapshot> entries,
        IReadOnlyDictionary<int, OptimizationRoomSnapshot> rooms)
    {
        var penalties = new List<decimal>();
        foreach (var staffGroup in entries.GroupBy(e => e.StaffId))
        {
            foreach (var dayGroup in staffGroup.GroupBy(e => e.DayOfWeek))
            {
                var ordered = dayGroup
                    .OrderBy(e => slotsStart(e.TimeSlotId))
                    .ToList();
                for (var i = 1; i < ordered.Count; i++)
                {
                    rooms.TryGetValue(ordered[i - 1].RoomId, out var a);
                    rooms.TryGetValue(ordered[i].RoomId, out var b);
                    if (a is null || b is null || a.BuildingId == b.BuildingId)
                        penalties.Add(5m);
                    else
                        penalties.Add(25m);
                }
            }
        }

        return penalties.Count == 0 ? 15m : Math.Round(penalties.Average(), 2);

        TimeSpan slotsStart(int slotId) => TimeSpan.Zero; // ordering fallback when slots missing
    }

    private static decimal EstimateAverageBreakMinutes(
        IReadOnlyList<OptimizationEntrySnapshot> entries,
        IReadOnlyDictionary<int, OptimizationSlotSnapshot> slots)
    {
        var gaps = new List<decimal>();
        foreach (var staffDay in entries.GroupBy(e => (e.StaffId, e.DayOfWeek)))
        {
            var ordered = staffDay
                .Select(e => slots.TryGetValue(e.TimeSlotId, out var s) ? s : null)
                .Where(s => s is not null)
                .OrderBy(s => s!.StartTime)
                .ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var gap = (decimal)(ordered[i]!.StartTime - ordered[i - 1]!.EndTime).TotalMinutes;
                if (gap > 0) gaps.Add(gap);
            }
        }

        return gaps.Count == 0 ? 15m : Math.Round(gaps.Average(), 2);
    }

    public static OptimizationContext WithWorkingState(
        OptimizationContext baseline,
        IReadOnlyList<OptimizationEntrySnapshot> working,
        IReadOnlyDictionary<string, decimal> metrics,
        int conflictCount) =>
        new()
        {
            TenantId = baseline.TenantId,
            AcademicYearId = baseline.AcademicYearId,
            TimetableId = baseline.TimetableId,
            DepartmentId = baseline.DepartmentId,
            EntryCount = working.Count,
            ConflictCount = conflictCount,
            BaselineMetrics = metrics,
            WorkingEntries = working,
            Rooms = baseline.Rooms,
            TimeSlots = baseline.TimeSlots,
            FacultyPreferredRoomIds = baseline.FacultyPreferredRoomIds,
            SubjectExpectedCapacities = baseline.SubjectExpectedCapacities
        };
}
