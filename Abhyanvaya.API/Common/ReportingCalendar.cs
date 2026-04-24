namespace Abhyanvaya.API.Common;

/// <summary>
/// Attendance "Date" values are UTC instants representing the start of a calendar day in the reporting zone (e.g. Asia/Kolkata).
/// Query params like <c>2026-04-24</c> bind as midnight UTC and must not be compared with <see cref="DateTime.Date"/> in UTC only.
/// </summary>
public static class ReportingCalendar
{
    /// <summary>
    /// Prefer IANA ids (e.g. Asia/Kolkata on Linux); fall back to Windows ids and UTC.
    /// </summary>
    public static TimeZoneInfo ResolveReportingTimeZone(string? configuredId)
    {
        var candidates = new[]
        {
            configuredId,
            "Asia/Kolkata",
            "India Standard Time",
        };

        foreach (var id in candidates)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    /// <summary>
    /// UTC [start, end) for the reporting-zone calendar day that contains <paramref name="utcInstant"/> (any instant that day).
    /// </summary>
    public static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) GetUtcRangeForReportingDayContainingUtc(
        DateTime utcInstant,
        TimeZoneInfo tz)
    {
        var utc = NormalizeToUtc(utcInstant);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        return GetUtcRangeForReportingCalendarDate(local.Year, local.Month, local.Day, tz);
    }

    /// <summary>
    /// UTC [start, end) for a reporting-zone calendar date (year/month/day in that zone).
    /// </summary>
    public static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) GetUtcRangeForReportingCalendarDate(
        int year,
        int month,
        int day,
        TimeZoneInfo tz)
    {
        var localMidnight = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localMidnight, tz);
        return (startUtc, startUtc.AddDays(1));
    }

    /// <summary>
    /// Start (inclusive) and end (exclusive) UTC bounds for "today" in <paramref name="tz"/>.
    /// </summary>
    public static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) GetReportingDayUtcRangeForUtcNow(
        DateTime utcNow,
        TimeZoneInfo tz)
    {
        return GetUtcRangeForReportingDayContainingUtc(utcNow, tz);
    }

    public static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc),
        };
    }
}
