namespace Abhyanvaya.Application.AttendanceRecovery;

/// <summary>
/// AI22.8.6.1 — operational SLA bands for pending attendance sessions.
/// Pure visibility; does not change workflow or AttendanceSessionResolver.
/// </summary>
public enum AttendanceSlaLevel
{
    Green = 0,
    Yellow = 1,
    Orange = 2,
    Red = 3
}

public sealed record AttendanceSlaSnapshot(
    AttendanceSlaLevel Level,
    string SlaStatus,
    double ElapsedMinutes,
    DateTime ExpectedCompletionUtc,
    string BadgeColor);

public static class AttendanceSlaCalculator
{
    public const double GreenMaxMinutes = 15;
    public const double YellowMaxMinutes = 30;
    public const double OrangeMaxMinutes = 60;

    public static AttendanceSlaSnapshot Calculate(double ageOrElapsedMinutes, double expectedRemainingMinutes)
    {
        var elapsed = Math.Max(0, ageOrElapsedMinutes);
        var level = elapsed switch
        {
            < GreenMaxMinutes => AttendanceSlaLevel.Green,
            < YellowMaxMinutes => AttendanceSlaLevel.Yellow,
            < OrangeMaxMinutes => AttendanceSlaLevel.Orange,
            _ => AttendanceSlaLevel.Red
        };

        var status = level switch
        {
            AttendanceSlaLevel.Green => "On Track",
            AttendanceSlaLevel.Yellow => "Watch",
            AttendanceSlaLevel.Orange => "At Risk",
            _ => "Breach"
        };

        var badge = level switch
        {
            AttendanceSlaLevel.Green => "success",
            AttendanceSlaLevel.Yellow => "warning",
            AttendanceSlaLevel.Orange => "secondary",
            _ => "error"
        };

        var expected = DateTime.UtcNow.AddMinutes(Math.Max(0, expectedRemainingMinutes));
        return new AttendanceSlaSnapshot(level, status, elapsed, expected, badge);
    }

    public static string FormatElapsed(double minutes)
    {
        if (minutes < 60) return $"{minutes:0}m";
        var h = Math.Floor(minutes / 60);
        var m = Math.Floor(minutes % 60);
        return $"{h:0}h {m:0}m";
    }
}
