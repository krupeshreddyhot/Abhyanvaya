using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>
/// Generic audit entry for session timeline views.
/// </summary>
public sealed class AuditEntryDto
{
    public long Id { get; init; }

    public string EntityName { get; init; } = string.Empty;

    public AuditAction Action { get; init; }

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public int? PerformedBy { get; init; }

    public string? PerformedByUsername { get; init; }

    public DateTime PerformedUtc { get; init; }
}
