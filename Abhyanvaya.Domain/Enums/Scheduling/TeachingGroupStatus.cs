namespace Abhyanvaya.Domain.Enums.Scheduling;

/// <summary>AI-SCHED-TG.3 — TeachingGroup lifecycle (distinct from TimetableStatus).</summary>
public enum TeachingGroupStatus : byte
{
    Draft = 1,
    Active = 2,
    Locked = 3,
    Archived = 4,
}
