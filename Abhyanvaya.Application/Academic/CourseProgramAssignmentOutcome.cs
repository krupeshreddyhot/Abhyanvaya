namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1D.24 Prompt 4B — result of authoritative Program assignment (for tests / orchestration).</summary>
public sealed record CourseProgramAssignmentOutcome(
    bool IsNoOp,
    bool ProgramIdChanged,
    int? PreviousProgramId,
    int? NewProgramId,
    int DomainEventsDispatched,
    int HierarchyCacheInvalidations,
    int StatisticsCacheInvalidations)
{
    public static CourseProgramAssignmentOutcome NoOp(int? programId) =>
        new(true, false, programId, programId, 0, 0, 0);
}
