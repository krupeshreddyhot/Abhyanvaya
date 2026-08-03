namespace Abhyanvaya.Domain.Enums.Scheduling;

/// <summary>Future strategy kinds. Phase 2B.6 registers contracts only — no algorithm implementations.</summary>
public enum OptimizationStrategyKind : byte
{
    None = 0,
    Greedy = 1,
    Genetic = 2,
    SimulatedAnnealing = 3,
    TabuSearch = 4,
    AiAssisted = 5,
    ManualAssisted = 6,
    WorkloadBalancing = 7,
    RoomOptimization = 8,
    PreferenceOptimization = 9,
    Pipeline = 10,
}

public enum OptimizationEngineRunStatus : byte
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Approved = 5,
    Rejected = 6,
}

public enum OptimizationSimulationStatus : byte
{
    Draft = 1,
    Previewed = 2,
    Scored = 3,
    Compared = 4,
    Rejected = 5,
    Accepted = 6, // Accepted for future apply only — never applies in 2B.6
}

public enum OptimizationDimension : byte
{
    FacultySatisfaction = 1,
    RoomUtilization = 2,
    TravelReduction = 3,
    WorkloadBalance = 4,
    PreferenceSatisfaction = 5,
    ConflictReduction = 6,
    StudentConvenience = 7,
}

public enum OptimizationMetricKind : byte
{
    FacultyUtilization = 1,
    RoomUtilization = 2,
    AverageTravel = 3,
    ConflictDensity = 4,
    AverageBreak = 5,
    WorkloadBalance = 6,
    PreferenceSatisfaction = 7,
    IdlePeriods = 8,
}

/// <summary>Sandbox scenario lifecycle (separate from timetable governance). Phase 2B.7.</summary>
public enum ScenarioStatus : byte
{
    Draft = 1,
    Saved = 2,
    Compared = 3,
    Reviewed = 4,
    Archived = 5,
}

public enum ScenarioHistoryAction : byte
{
    Created = 1,
    Modified = 2,
    Viewed = 3,
    Compared = 4,
    Favorited = 5,
    Archived = 6,
    Replayed = 7,
    Duplicated = 8,
    Renamed = 9,
    Deleted = 10,
    Commented = 11,
    Shared = 12,
    ApprovalRequested = 13,
    Tagged = 14,
    Pinned = 15,
}
