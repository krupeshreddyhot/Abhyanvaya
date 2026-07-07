namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>
/// Result of materializing official attendance from a reviewed AI session.
/// </summary>
public sealed class AttendanceBuildSummaryDto
{
    public Guid AttendanceSessionId { get; set; }

    public int Present { get; set; }

    public int Absent { get; set; }

    public int Ignored { get; set; }

    public int Rejected { get; set; }

    public int Unknown { get; set; }

    public int ManualCorrections { get; set; }

    public int TotalStudents { get; set; }

    public DateTime? GeneratedUtc { get; set; }

    public int? DurationMilliseconds { get; set; }

    public bool AlreadyFinalized { get; set; }
}
