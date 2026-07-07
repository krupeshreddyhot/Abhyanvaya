using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Capture metadata for an official <see cref="Attendance"/> row produced from session-based attendance.
/// </summary>
public class AttendanceDetail : BaseEntity
{
    public required int AttendanceId { get; set; }

    /// <summary>Source AI recognition when attendance was materialized from a session.</summary>
    public Guid? AttendanceRecognitionId { get; set; }

    public AttendanceMethod CaptureMethod { get; set; } = AttendanceMethod.AIPhoto;

    public decimal? ConfidenceScore { get; set; }

    public bool TeacherOverride { get; set; }

    public int? FaceNumber { get; set; }

    /// <summary>
    /// Immutable JSON snapshot of recognition and session AI metadata at attendance materialization time.
    /// Populated only when official attendance is built from a reviewed AI session.
    /// </summary>
    public string? RecognitionSnapshotJson { get; set; }

    public Attendance Attendance { get; set; } = null!;

    public AttendanceRecognition? AttendanceRecognition { get; set; }
}
