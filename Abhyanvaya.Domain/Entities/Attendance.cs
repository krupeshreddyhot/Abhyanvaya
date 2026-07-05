using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

public class Attendance : BaseEntity
{
    public required int StudentId { get; set; }

    public required int SubjectId { get; set; }

    public DateTime Date { get; set; }

    public AttendanceStatus Status { get; set; }

    public bool IsLocked { get; set; }

    public Guid? AttendanceSessionId { get; set; }

    public Student? Student { get; set; }

    public Subject Subject { get; set; } = null!;

    /// <summary>Parent session for AI or session-based capture; null for legacy manual attendance.</summary>
    public AttendanceSession? AttendanceSession { get; set; }

    /// <summary>Capture metadata when attendance is materialized from a session.</summary>
    public AttendanceDetail? Detail { get; set; }
}
