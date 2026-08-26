namespace Abhyanvaya.Domain.Enums.Scheduling;

/// <summary>AI-SCHED-TG.3 — Instructional activity kind (Lecture vs Lab compatibility).</summary>
public enum TeachingGroupActivityKind : byte
{
    Lecture = 1,
    Laboratory = 2,
    Tutorial = 3,
    Seminar = 4,
    Other = 5,
}
