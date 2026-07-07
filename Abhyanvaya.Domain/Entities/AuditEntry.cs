using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Generic audit record for entity changes across modules (Student, Attendance, Timetable, etc.).
/// Specialized audit tables such as <see cref="AttendanceRecognitionReviewHistory"/> remain separate.
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    public required string EntityName { get; set; }

    public required string EntityId { get; set; }

    public AuditAction Action { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public int? PerformedBy { get; set; }

    public DateTime PerformedUtc { get; set; }
}
