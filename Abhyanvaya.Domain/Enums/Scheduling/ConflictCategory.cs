namespace Abhyanvaya.Domain.Enums.Scheduling;

/// <summary>AI30 Phase 2B conflict classification. Detection only — never auto-fixes.</summary>
public enum ConflictCategory : byte
{
    Faculty = 1,
    Room = 2,
    Student = 3,
    Calendar = 4,
    Other = 99,
}
