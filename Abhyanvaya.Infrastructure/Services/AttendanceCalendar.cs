using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Abhyanvaya.Infrastructure.Services;

/// <summary>
/// Reporting-zone aware implementation of <see cref="IAttendanceCalendar"/>.
/// <para>
/// Resolves the reporting time zone once (via <c>Dashboard:ReportingTimeZoneId</c>, defaulting to Asia/Kolkata)
/// and delegates all day-boundary math to <see cref="AttendanceDay"/> — the single canonical implementation of
/// "reporting calendar day → UTC instant range" shared across the manual attendance endpoint, the photo/AI
/// attendance pipeline, and reporting reads.
/// </para>
/// </summary>
public sealed class AttendanceCalendar : IAttendanceCalendar
{
    private readonly TimeZoneInfo _reportingZone;

    public AttendanceCalendar(IConfiguration configuration)
    {
        _reportingZone = ResolveReportingTimeZone(configuration["Dashboard:ReportingTimeZoneId"]);
    }

    /// <inheritdoc />
    public AttendanceDay GetAttendanceDay(DateTime date) =>
        AttendanceDay.FromReportingCalendar(date, _reportingZone);

    /// <inheritdoc />
    public AttendanceDay Today() => AttendanceDay.Today(_reportingZone);

    /// <inheritdoc />
    public AttendanceDay ForCalendarDate(int year, int month, int day) =>
        AttendanceDay.FromDate(new DateOnly(year, month, day), _reportingZone);

    private static TimeZoneInfo ResolveReportingTimeZone(string? configuredId)
    {
        var candidates = new[] { configuredId, "Asia/Kolkata", "India Standard Time" };

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
}
