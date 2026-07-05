namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>
/// Post-finalization attendance session report (no PDF).
/// </summary>
public sealed class AttendanceSessionReportDto
{
    public Guid AttendanceSessionId { get; init; }

    public int Present { get; init; }

    public int Absent { get; init; }

    public decimal? RecognitionAccuracy { get; init; }

    public int ManualCorrections { get; init; }

    public int? ReviewTimeMilliseconds { get; init; }

    public DateTime? FinalizationTime { get; init; }
}
