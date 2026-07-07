namespace Abhyanvaya.Domain.ValueObjects;

/// <summary>
/// Immutable domain value object representing one reporting-zone calendar day for attendance purposes.
/// <para>
/// Attendance is a business concept ("today's class"), not a raw UTC timestamp. Prior to this value object,
/// every attendance-adjacent service (<c>AttendanceController</c>, <c>AttendanceBuilder</c>,
/// <c>AttendanceSessionQueryService</c>, <c>ReportingCalendar</c>, <c>AttendanceCalendar</c>) independently
/// re-implemented the same "convert a calendar day, in a given reporting time zone, to a UTC instant range"
/// calculation. Any drift between those implementations is exactly what caused a prior production incident
/// (two attendance-capture paths anchoring "the same day" to two different UTC instants, producing duplicate
/// rows). <see cref="AttendanceDay"/> centralizes that calculation so there is exactly one implementation.
/// </para>
/// <para>
/// This type intentionally has zero dependency on EF Core, ASP.NET Core, or any infrastructure concern — only
/// the base class library (<see cref="DateOnly"/>, <see cref="DateTime"/>, <see cref="TimeZoneInfo"/>). Only the
/// infrastructure layer decides *which* <see cref="TimeZoneInfo"/> is "the reporting zone" (via configuration)
/// and converts an <see cref="AttendanceDay"/> into the <see cref="DateTime"/> stored in
/// <c>Attendance.Date</c> (<see cref="UtcStart"/>). The database column and its type are unchanged.
/// </para>
/// </summary>
public sealed class AttendanceDay : IEquatable<AttendanceDay>
{
    /// <summary>The calendar date in <see cref="ReportingTimeZone"/> that this instance represents.</summary>
    public DateOnly LocalDate { get; }

    /// <summary>The time zone this reporting day is anchored to (e.g. Asia/Kolkata).</summary>
    public TimeZoneInfo ReportingTimeZone { get; }

    /// <summary>
    /// UTC instant marking the start (inclusive) of this reporting day. This is the canonical value to persist
    /// in <c>Attendance.Date</c> — both manual and AI/photo attendance must store exactly this value.
    /// </summary>
    public DateTime UtcStart { get; }

    /// <summary>UTC instant marking the end (exclusive) of this reporting day — <see cref="UtcStart"/> + 24h.</summary>
    public DateTime UtcEnd { get; }

    private AttendanceDay(DateOnly localDate, TimeZoneInfo reportingTimeZone, DateTime utcStart, DateTime utcEnd)
    {
        LocalDate = localDate;
        ReportingTimeZone = reportingTimeZone;
        UtcStart = utcStart;
        UtcEnd = utcEnd;
    }

    /// <summary>Builds the reporting day for an explicit local calendar date.</summary>
    public static AttendanceDay FromDate(DateOnly localDate, TimeZoneInfo reportingTimeZone)
    {
        ArgumentNullException.ThrowIfNull(reportingTimeZone);

        var localMidnight = new DateTime(
            localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localMidnight, reportingTimeZone);
        return new AttendanceDay(localDate, reportingTimeZone, utcStart, utcStart.AddDays(1));
    }

    /// <summary>
    /// Builds the reporting day that contains the given UTC instant (e.g. any timestamp already known to be UTC).
    /// </summary>
    public static AttendanceDay FromUtc(DateTime utcInstant, TimeZoneInfo reportingTimeZone)
    {
        ArgumentNullException.ThrowIfNull(reportingTimeZone);

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc), reportingTimeZone);
        return FromDate(DateOnly.FromDateTime(local), reportingTimeZone);
    }

    /// <summary>
    /// Builds the reporting day for a <see cref="DateTime"/> of unknown/mixed origin — e.g. a value bound from an
    /// HTTP query parameter or JSON body, which may arrive as <see cref="DateTimeKind.Unspecified"/>,
    /// <see cref="DateTimeKind.Local"/>, or <see cref="DateTimeKind.Utc"/>. Normalizes first, then resolves the
    /// containing reporting day. This replaces the previously duplicated
    /// <c>ReportingCalendar.NormalizeToUtc</c> / <c>AttendanceCalendar</c> normalization logic.
    /// </summary>
    public static AttendanceDay FromReportingCalendar(DateTime value, TimeZoneInfo reportingTimeZone) =>
        FromUtc(NormalizeToUtc(value), reportingTimeZone);

    /// <summary>Builds the reporting day containing the current instant.</summary>
    public static AttendanceDay Today(TimeZoneInfo reportingTimeZone) =>
        FromUtc(DateTime.UtcNow, reportingTimeZone);

    /// <summary>True if the given instant (of any <see cref="DateTimeKind"/>) falls within this reporting day.</summary>
    public bool Contains(DateTime utc)
    {
        var normalized = NormalizeToUtc(utc);
        return normalized >= UtcStart && normalized < UtcEnd;
    }

    /// <summary>
    /// Mirrors the normalization previously duplicated across <c>ReportingCalendar.NormalizeToUtc</c> and
    /// <c>AttendanceCalendar</c>: UTC values pass through, Local values convert, and Unspecified values are
    /// treated as already-UTC (never silently reinterpreted as local time). Preserved exactly for
    /// backward-compatibility with existing call sites.
    /// </summary>
    private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public bool Equals(AttendanceDay? other) =>
        other is not null
        && LocalDate == other.LocalDate
        && string.Equals(ReportingTimeZone.Id, other.ReportingTimeZone.Id, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AttendanceDay other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(LocalDate, ReportingTimeZone.Id);

    public override string ToString() => $"{LocalDate:yyyy-MM-dd} [{ReportingTimeZone.Id}]";

    public static bool operator ==(AttendanceDay? left, AttendanceDay? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(AttendanceDay? left, AttendanceDay? right) => !(left == right);
}
