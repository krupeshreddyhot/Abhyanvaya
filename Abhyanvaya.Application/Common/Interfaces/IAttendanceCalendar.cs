using Abhyanvaya.Domain.ValueObjects;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Resolves the canonical <see cref="AttendanceDay"/> for the configured reporting time zone.
/// <para>
/// Both manual attendance (HTTP) and photo/AI attendance (background pipeline) must agree on the exact
/// <c>Attendance.Date</c> value they persist for a given calendar day, otherwise the same student can end up
/// with two rows for one subject/day. This service is the single place that knows "which time zone is the
/// reporting zone" (via configuration) and turns a raw date/instant into an <see cref="AttendanceDay"/>, so both
/// paths — and every other attendance-adjacent read (locks, existing-attendance lookups, reports) — are
/// guaranteed to use identical day boundaries.
/// </para>
/// </summary>
public interface IAttendanceCalendar
{
    /// <summary>
    /// Resolves the <see cref="AttendanceDay"/> containing <paramref name="date"/>. Accepts any
    /// <see cref="DateTime"/> (UTC, Local, or Unspecified — e.g. straight from an HTTP query/body binder or a
    /// stored calendar-date column read back as Unspecified) and normalizes it consistently.
    /// </summary>
    AttendanceDay GetAttendanceDay(DateTime date);

    /// <summary>Resolves the <see cref="AttendanceDay"/> for "now" in the configured reporting time zone.</summary>
    AttendanceDay Today();

    /// <summary>
    /// Resolves the <see cref="AttendanceDay"/> for an explicit reporting-zone calendar date (year/month/day),
    /// e.g. building a month's [start, end) range for reports. Unlike <see cref="GetAttendanceDay"/>, the
    /// inputs are unambiguous calendar-date components rather than an instant that must be normalized first.
    /// </summary>
    AttendanceDay ForCalendarDate(int year, int month, int day);
}
