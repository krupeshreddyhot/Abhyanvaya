namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>
/// Read-only finalization readiness snapshot for teacher review.
/// </summary>
public sealed class FinalizationStatusDto
{
    public Guid AttendanceSessionId { get; init; }

    public bool CanFinalize { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = [];

    public int PendingRecognitions { get; init; }

    public int ReviewedRecognitions { get; init; }

    public int ManualOverrides { get; init; }

    public int RejectedRecognitions { get; init; }

    public int UnknownFaces { get; init; }

    public bool AttendanceAlreadyGenerated { get; init; }

    public int StudentsPresent { get; init; }

    public int StudentsAbsent { get; init; }

    public int TotalStudents { get; init; }

    public bool ReadyToFinalize => CanFinalize;

    public DateTime AttendanceDate { get; init; }

    public string? FacultyName { get; init; }

    public string? SubjectName { get; init; }
}
