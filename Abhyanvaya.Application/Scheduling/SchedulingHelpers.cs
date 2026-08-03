using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>Detects overlapping time intervals within the same set and day scope.</summary>
public static class TimeSlotOverlapHelper
{
    public static TimeSlotInterval? FindOverlap(IEnumerable<(int Id, TimeSlotInterval Interval)> existing, TimeSlotInterval candidate)
    {
        foreach (var (id, slot) in existing)
        {
            if (candidate.ExcludeId.HasValue && id == candidate.ExcludeId.Value)
                continue;

            if (!SameDayScope(slot.DayOfWeek, candidate.DayOfWeek))
                continue;

            if (IntervalsOverlap(slot.StartTime, slot.EndTime, candidate.StartTime, candidate.EndTime))
                return slot;
        }

        return null;
    }

    public static bool HasOverlap(IEnumerable<(int Id, TimeSlotInterval Interval)> existing, TimeSlotInterval candidate) =>
        FindOverlap(existing, candidate) is not null;

    public static bool HasDuplicatePeriodNumber(IEnumerable<(int Id, TimeSlotInterval Interval)> existing, TimeSlotInterval candidate)
    {
        if (!candidate.PeriodNumber.HasValue)
            return false;

        return existing.Any(entry =>
            (!candidate.ExcludeId.HasValue || entry.Id != candidate.ExcludeId.Value)
            && SameDayScope(entry.Interval.DayOfWeek, candidate.DayOfWeek)
            && entry.Interval.PeriodNumber == candidate.PeriodNumber);
    }

    internal static bool SameDayScope(byte? left, byte? right) =>
        left == right || left is null || right is null;

    internal static bool IntervalsOverlap(TimeSpan startA, TimeSpan endA, TimeSpan startB, TimeSpan endB) =>
        startA < endB && startB < endA;
}

/// <summary>Shifts calendar dates when cloning an academic year.</summary>
public static class AcademicYearCloneHelper
{
    public static DateOnly ShiftDate(DateOnly sourceDate, DateOnly sourceYearStart, DateOnly targetYearStart)
    {
        var deltaDays = targetYearStart.DayNumber - sourceYearStart.DayNumber;
        return sourceDate.AddDays(deltaDays);
    }
}

/// <summary>Detects overlapping date and slot ranges for faculty/room availability.</summary>
public static class AvailabilityOverlapHelper
{
    public static bool DateRangesOverlap(DateOnly startA, DateOnly endA, DateOnly startB, DateOnly endB) =>
        startA <= endB && startB <= endA;

    /// <summary>
    /// Returns true when slot ranges overlap. Null slot ids on either side mean all-day coverage for overlapping dates.
    /// </summary>
    public static bool SlotRangesOverlap(
        int? startSlotIdA, int? endSlotIdA, TimeSpan? startTimeA, TimeSpan? endTimeA,
        int? startSlotIdB, int? endSlotIdB, TimeSpan? startTimeB, TimeSpan? endTimeB)
    {
        var allDayA = !startSlotIdA.HasValue && !endSlotIdA.HasValue;
        var allDayB = !startSlotIdB.HasValue && !endSlotIdB.HasValue;
        if (allDayA || allDayB)
            return true;

        if (startTimeA.HasValue && endTimeA.HasValue && startTimeB.HasValue && endTimeB.HasValue)
            return TimeSlotOverlapHelper.IntervalsOverlap(startTimeA.Value, endTimeA.Value, startTimeB.Value, endTimeB.Value);

        return true;
    }

    public static bool HasOverlap(
        DateOnly startA, DateOnly endA, int? startSlotIdA, int? endSlotIdA, TimeSpan? startTimeA, TimeSpan? endTimeA,
        DateOnly startB, DateOnly endB, int? startSlotIdB, int? endSlotIdB, TimeSpan? startTimeB, TimeSpan? endTimeB)
    {
        if (!DateRangesOverlap(startA, endA, startB, endB))
            return false;

        return SlotRangesOverlap(startSlotIdA, endSlotIdA, startTimeA, endTimeA, startSlotIdB, endSlotIdB, startTimeB, endTimeB);
    }
}
