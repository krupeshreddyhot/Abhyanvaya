namespace Abhyanvaya.Domain.Enums.Scheduling;

/// <summary>AI-SCHED-TG.3 — How TeachingGroup student membership is obtained.</summary>
public enum TeachingGroupMembershipSource : byte
{
    Section = 1,
    CombinedSections = 2,
    StudentSubject = 3,
    ExplicitStudents = 4,
    Hybrid = 5,
}
