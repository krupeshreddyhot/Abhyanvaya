namespace Abhyanvaya.Domain.Enums.Scheduling;

/// <summary>AI-SCHED-TG.3 — Operational teaching cohort type (not an academic Section).</summary>
public enum TeachingGroupType : byte
{
    SectionDerived = 1,
    CombinedSections = 2,
    StudentSubset = 3,
    Elective = 4,
    Laboratory = 5,
    CapacitySplit = 6,
    Custom = 7,
}
