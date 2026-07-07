namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>Request to create a draft AI photo attendance session.</summary>
public sealed class CreatePhotoAttendanceSessionDto
{
    public int CourseId { get; init; }

    public int GroupId { get; init; }

    public int SemesterId { get; init; }

    public int SubjectId { get; init; }

    public DateTime AttendanceDate { get; init; }

    public int PeriodNumber { get; init; }

    public short SessionNumber { get; init; } = 1;

    public int TotalStudents { get; init; }
}

public sealed class CreatePhotoAttendanceSessionResponseDto
{
    public Guid AttendanceSessionId { get; init; }
}
