using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Rules;

public sealed class CalendarHolidayRule : IConflictRule
{
    public string RuleCode => "CALENDAR_HOLIDAY";
    public string RuleName => "Holiday";
    public ConflictCategory Category => ConflictCategory.Calendar;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        var blocking = context.Holidays.Where(h => !h.IsWorkingDayOverride).ToList();
        if (blocking.Count == 0) return Task.CompletedTask;

        // Weekly grid cannot map absolute dates; emit year-level guidance + per non-working alignment
        foreach (var holiday in blocking.Take(5))
        {
            var dow = (byte)holiday.Date.DayOfWeek;
            foreach (var entry in context.Entries.Where(e => e.DayOfWeek == dow).Take(3))
            {
                bag.Add(context.Create(
                    this,
                    holiday.RequiresRescheduling ? ConflictSeverity.Error : ConflictSeverity.Warning,
                    $"Holiday '{holiday.Name}' falls on weekday {dow} used by this timetable.",
                    $"Holiday date {holiday.Date:yyyy-MM-dd}; RequiresRescheduling={holiday.RequiresRescheduling}.",
                    "Plan rescheduling or mark an override working day if classes must run.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class CalendarWorkingDayRule : IConflictRule
{
    public string RuleCode => "CALENDAR_WORKING_DAY";
    public string RuleName => "Working Day";
    public ConflictCategory Category => ConflictCategory.Calendar;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (!context.WorkingDays.TryGetValue(entry.DayOfWeek, out var wd))
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Warning,
                    "No working-day configuration exists for this weekday.",
                    $"DayOfWeek {entry.DayOfWeek} missing in academic year working days.",
                    "Configure working days for the academic year.",
                    entry));
                continue;
            }

            if (!wd.IsWorking)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Error,
                    "Entry uses a calendar non-working day.",
                    "Academic calendar marks this weekday as non-working.",
                    "Remove or move entries off non-working days.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class CalendarSemesterRule : IConflictRule
{
    public string RuleCode => "CALENDAR_SEMESTER";
    public string RuleName => "Semester";
    public ConflictCategory Category => ConflictCategory.Calendar;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries.Where(e => e.SemesterId <= 0))
        {
            bag.Add(context.Create(
                this,
                ConflictSeverity.Warning,
                "Timetable entry is missing a valid semester reference.",
                "SemesterId is unset or invalid on the entry.",
                "Ensure subject allocation carries a Catalog semester.",
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class CalendarAcademicYearRule : IConflictRule
{
    public string RuleCode => "CALENDAR_ACADEMIC_YEAR";
    public string RuleName => "Academic Year";
    public ConflictCategory Category => ConflictCategory.Calendar;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        if (context.AcademicYear is null)
        {
            // Cannot attach to entry-less context meaningfully
            return Task.CompletedTask;
        }

        if (context.AcademicYear.EndDate < context.AcademicYear.StartDate)
        {
            foreach (var entry in context.Entries.Take(1))
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Critical,
                    "Academic year end date is before start date.",
                    $"Year {context.AcademicYear.Name}: {context.AcademicYear.StartDate:d}–{context.AcademicYear.EndDate:d}.",
                    "Correct academic year dates in the calendar setup.",
                    entry));
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class CalendarClosedCampusRule : IConflictRule
{
    public string RuleCode => "CALENDAR_CLOSED_CAMPUS";
    public string RuleName => "Closed Campus";
    public ConflictCategory Category => ConflictCategory.Calendar;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            var campusId = context.CampusIdForRoom(entry.RoomId);
            if (!campusId.HasValue) continue;
            if (!context.Campuses.TryGetValue(campusId.Value, out var campus)) continue;
            if (campus.IsActive) continue;

            bag.Add(context.Create(
                this,
                ConflictSeverity.Critical,
                $"Campus '{campus.Name}' is inactive/closed but still has scheduled classes.",
                "Room resolves to an inactive campus.",
                "Move classes to an active campus or reactivate the campus.",
                entry));
        }
        return Task.CompletedTask;
    }
}

public sealed class CalendarHolidayTypeRule : IConflictRule
{
    public string RuleCode => "CALENDAR_HOLIDAY_TYPE";
    public string RuleName => "Holiday Types";
    public ConflictCategory Category => ConflictCategory.Calendar;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        var missingType = context.Holidays.Where(h => h.RequiresRescheduling && h.HolidayTypeCatalogId is null).ToList();
        if (missingType.Count == 0) return Task.CompletedTask;

        foreach (var entry in context.Entries.Take(1))
        {
            bag.Add(context.Create(
                this,
                ConflictSeverity.Information,
                $"{missingType.Count} rescheduling holiday(s) lack a holiday type catalog link.",
                "Holiday type metadata is incomplete for calendar validation.",
                "Assign holiday types from the holiday type catalog.",
                entry));
        }
        return Task.CompletedTask;
    }
}
