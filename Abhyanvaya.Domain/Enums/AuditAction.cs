namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Generic audit action for cross-module entity change tracking.
/// </summary>
public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3,
    Restored = 4,
    Approved = 5,
    Cancelled = 6,
    Reviewed = 7,
    Custom = 99
}
