namespace Abhyanvaya.Domain.Enums.Scheduling;

/// <summary>
/// Severity for conflict findings. Even <see cref="Critical"/> is non-blocking for editing in Phase 2B.
/// </summary>
public enum ConflictSeverity : byte
{
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
}
