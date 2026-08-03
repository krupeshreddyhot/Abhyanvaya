using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Rules;

public sealed class FacultyDoubleBookingRule : IConflictRule
{
    public string RuleCode => "FACULTY_DOUBLE_BOOKING";
    public string RuleName => "Faculty Double Booking";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        var groups = context.Entries.GroupBy(e => new { e.StaffId, e.DayOfWeek, e.TimeSlotId }).Where(g => g.Count() > 1);
        foreach (var group in groups)
        {
            var list = group.ToList();
            foreach (var entry in list)
            {
                var other = list.First(x => x.Id != entry.Id);
                var name = context.StaffNames.GetValueOrDefault(entry.StaffId, $"Staff {entry.StaffId}");
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Critical,
                    $"{name} is double-booked in the same day and time slot.",
                    $"Entries {entry.Id} and {other.Id} both assign staff {entry.StaffId} to day {entry.DayOfWeek} slot {entry.TimeSlotId}.",
                    "Move one of the classes to a different period or assign another faculty member.",
                    entry,
                    other.Id));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class FacultyAvailabilityRule : IConflictRule
{
    public string RuleCode => "FACULTY_AVAILABILITY";
    public string RuleName => "Faculty Availability";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            var hits = context.FacultyAvailabilities.Where(a =>
                a.StaffId == entry.StaffId &&
                (a.AvailabilityType == FacultyAvailabilityType.Unavailable || a.AvailabilityType == FacultyAvailabilityType.ApprovedLeave)).ToList();
            if (hits.Count == 0) continue;

            var name = context.StaffNames.GetValueOrDefault(entry.StaffId, $"Staff {entry.StaffId}");
            bag.Add(context.Create(
                this,
                ConflictSeverity.Error,
                $"{name} has unavailable/leave records overlapping this academic year assignment.",
                $"Faculty availability type(s): {string.Join(", ", hits.Select(h => h.AvailabilityType.ToString()).Distinct())}.",
                "Reassign the class or update faculty availability for this academic year.",
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class FacultyPreferenceRule : IConflictRule
{
    public string RuleCode => "FACULTY_PREFERENCE";
    public string RuleName => "Faculty Preference";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            var pref = context.FacultyPreferences
                .Where(p => p.StaffId == entry.StaffId)
                .OrderByDescending(p => p.Priority)
                .FirstOrDefault();
            if (pref is null) continue;

            if (pref.PreferredWorkingDaysFlags != 0)
            {
                var flag = (byte)(1 << entry.DayOfWeek);
                if ((pref.PreferredWorkingDaysFlags & flag) == 0)
                {
                    bag.Add(context.Create(
                        this,
                        ConflictSeverity.Warning,
                        "Class is scheduled on a day outside faculty preferred working days.",
                        $"PreferredWorkingDaysFlags={pref.PreferredWorkingDaysFlags}, entry day={entry.DayOfWeek}.",
                        "Move the class to a preferred working day when possible.",
                        entry));
                }
            }

            if (pref.PreferredRoomId.HasValue && pref.PreferredRoomId.Value != entry.RoomId)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Information,
                    "Assigned room differs from faculty preferred room.",
                    $"Preferred room {pref.PreferredRoomId}, assigned {entry.RoomId}.",
                    "Consider moving the class to the preferred room if available.",
                    entry));
            }

            if (pref.PreferredDepartmentId.HasValue && pref.PreferredDepartmentId.Value != entry.DepartmentId)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Information,
                    "Entry department differs from faculty preferred department.",
                    $"Preferred department {pref.PreferredDepartmentId}, entry department {entry.DepartmentId}.",
                    "Review allocation or faculty preference alignment.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class FacultyMaximumContinuousClassesRule : IConflictRule
{
    public string RuleCode => "FACULTY_MAX_CONTINUOUS";
    public string RuleName => "Maximum Continuous Classes";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var staffDay in context.Entries.GroupBy(e => new { e.StaffId, e.DayOfWeek }))
        {
            var ordered = staffDay
                .Select(e => (Entry: e, Slot: context.TimeSlots.GetValueOrDefault(e.TimeSlotId)))
                .Where(x => x.Slot is not null && x.Slot.SlotKind == SlotKind.Period)
                .OrderBy(x => x.Slot!.StartTime)
                .ToList();
            if (ordered.Count < 2) continue;

            var pref = context.FacultyPreferences.Where(p => p.StaffId == staffDay.Key.StaffId)
                .OrderByDescending(p => p.Priority).FirstOrDefault();
            var maxContinuous = pref?.MaximumContinuousClasses > 0
                ? pref.MaximumContinuousClasses
                : context.Thresholds.MaximumContinuousClasses;

            var run = 1;
            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1].Slot!;
                var cur = ordered[i].Slot!;
                var contiguous = prev.EndTime == cur.StartTime ||
                                 (ordered[i - 1].Entry.TimeSlotId != cur.Id &&
                                  context.TimeSlots.Values.Any(s =>
                                      s.SlotKind is SlotKind.Break or SlotKind.Lunch &&
                                      s.StartTime >= prev.EndTime && s.EndTime <= cur.StartTime) == false &&
                                  cur.StartTime >= prev.EndTime &&
                                  (cur.StartTime - prev.EndTime).TotalMinutes <= context.Thresholds.ContiguousGapMinutes);

                if (contiguous) run++;
                else run = 1;

                if (run > maxContinuous)
                {
                    var entry = ordered[i].Entry;
                    bag.Add(context.Create(
                        this,
                        ConflictSeverity.Warning,
                        $"Faculty exceeds maximum continuous classes ({maxContinuous}).",
                        $"Detected continuous run of {run} periods on day {entry.DayOfWeek}.",
                        "Insert a break or redistribute consecutive classes.",
                        entry));
                }
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class FacultyBreakViolationRule : IConflictRule
{
    public string RuleCode => "FACULTY_BREAK_VIOLATION";
    public string RuleName => "Break Violations";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var staffDay in context.Entries.GroupBy(e => new { e.StaffId, e.DayOfWeek }))
        {
            var pref = context.FacultyPreferences.Where(p => p.StaffId == staffDay.Key.StaffId)
                .OrderByDescending(p => p.Priority).FirstOrDefault();
            var minBreak = pref?.MinimumBreakBetweenClasses > 0
                ? pref.MinimumBreakBetweenClasses
                : context.Thresholds.MinimumBreakMinutes;
            if (minBreak <= 0) continue;

            var ordered = staffDay
                .Select(e => (Entry: e, Slot: context.TimeSlots.GetValueOrDefault(e.TimeSlotId)))
                .Where(x => x.Slot is not null)
                .OrderBy(x => x.Slot!.StartTime)
                .ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                var gap = (ordered[i].Slot!.StartTime - ordered[i - 1].Slot!.EndTime).TotalMinutes;
                if (gap >= 0 && gap < minBreak)
                {
                    bag.Add(context.Create(
                        this,
                        ConflictSeverity.Warning,
                        $"Break between classes ({gap:0} min) is below preferred minimum ({minBreak} min).",
                        "Consecutive periods leave insufficient rest based on faculty preference.",
                        "Increase the gap between consecutive classes for this faculty.",
                        ordered[i].Entry,
                        ordered[i - 1].Entry.Id));
                }
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class FacultyCrossCampusTravelRule : IConflictRule
{
    public string RuleCode => "FACULTY_CROSS_CAMPUS";
    public string RuleName => "Cross Campus Travel";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var staffDay in context.Entries.GroupBy(e => new { e.StaffId, e.DayOfWeek }))
        {
            var ordered = staffDay
                .Select(e => (Entry: e, Slot: context.TimeSlots.GetValueOrDefault(e.TimeSlotId), CampusId: context.CampusIdForRoom(e.RoomId)))
                .Where(x => x.Slot is not null && x.CampusId.HasValue)
                .OrderBy(x => x.Slot!.StartTime)
                .ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].CampusId == ordered[i - 1].CampusId) continue;
                var gap = (ordered[i].Slot!.StartTime - ordered[i - 1].Slot!.EndTime).TotalMinutes;
                var travelBuffer = context.Thresholds.FacultyTravelBufferMinutes;
                if (gap < travelBuffer)
                {
                    bag.Add(context.Create(
                        this,
                        ConflictSeverity.Error,
                        "Faculty must travel between campuses with insufficient transition time.",
                        $"Campus changed from {ordered[i - 1].CampusId} to {ordered[i].CampusId} with only {gap:0} minutes gap.",
                        $"Allow at least {travelBuffer} minutes or avoid consecutive cross-campus assignments.",
                        ordered[i].Entry,
                        ordered[i - 1].Entry.Id));
                }
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class FacultyLunchViolationRule : IConflictRule
{
    public string RuleCode => "FACULTY_LUNCH_VIOLATION";
    public string RuleName => "Lunch Violations";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        if (!context.Thresholds.LunchWindowEnabled) return Task.CompletedTask;
        var lunchSlots = context.TimeSlots.Values.Where(s => s.SlotKind == SlotKind.Lunch).ToList();
        if (lunchSlots.Count == 0) return Task.CompletedTask;

        foreach (var entry in context.Entries)
        {
            if (!context.TimeSlots.TryGetValue(entry.TimeSlotId, out var slot)) continue;
            var overlapsLunch = lunchSlots.Any(l =>
                (!l.DayOfWeek.HasValue || l.DayOfWeek == entry.DayOfWeek) &&
                slot.StartTime < l.EndTime && l.StartTime < slot.EndTime);
            if (!overlapsLunch) continue;

            bag.Add(context.Create(
                this,
                ConflictSeverity.Warning,
                "Class overlaps a designated lunch slot.",
                $"Entry slot {slot.Name} overlaps lunch window.",
                "Move the class outside the lunch period.",
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class FacultyWorkingDayViolationRule : IConflictRule
{
    public string RuleCode => "FACULTY_WORKING_DAY";
    public string RuleName => "Working Day Violations";
    public ConflictCategory Category => ConflictCategory.Faculty;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (context.WorkingDays.TryGetValue(entry.DayOfWeek, out var wd) && !wd.IsWorking)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Error,
                    "Class is scheduled on a non-working day.",
                    $"Working day flag is false for day {entry.DayOfWeek}.",
                    "Move the class to a configured working day.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}
