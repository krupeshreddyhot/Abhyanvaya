using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.1C.5 — Immutable scenario revision (never overwrite). AI29.1C.5A adds Operation + canonical checksum.</summary>
public class AllocationScenarioVersion : BaseEntity
{
    public Guid ScenarioId { get; set; }
    public int VersionNumber { get; set; }
    public string ContextVersion { get; set; } = "";
    public string ContextChecksum { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public string Reason { get; set; } = "";
    /// <summary>AI29.1C.5A — Governance operation that produced this version.</summary>
    public string Operation { get; set; } = "";
    public string StrategyConfigurationVersion { get; set; } = "1";
    public string ConstraintConfigurationVersion { get; set; } = "1";
    public double Score { get; set; }
    /// <summary>Lifecycle status at version creation time.</summary>
    public string Status { get; set; } = "";
    public string Checksum { get; set; } = "";
    public string ScenarioJson { get; set; } = "{}";
    public string ConfigJson { get; set; } = "{}";
    public string TraceJson { get; set; } = "[]";
}

/// <summary>AI29.1C.5 — Operational audit trail (no unnecessary PII).</summary>
public class AllocationAuditEntry : BaseEntity
{
    public Guid AuditId { get; set; }
    public string Action { get; set; } = "";
    public Guid? ScenarioId { get; set; }
    public Guid? SessionId { get; set; }
    public int? VersionNumber { get; set; }
    public string? ContextVersion { get; set; }
    public string Result { get; set; } = "";
    public string? Detail { get; set; }
    public DateTime OccurredAt { get; set; }
    public int? ActorUserId { get; set; }
}

/// <summary>AI29.1C.5 — Scenario lifecycle statuses (authoritative governance machine; AI29.1C.5A).</summary>
public static class AllocationScenarioLifecycle
{
    public const string Draft = "Draft";
    public const string Saved = "Saved";
    public const string Simulated = "Simulated";
    public const string Compared = "Compared";
    public const string Reviewed = "Reviewed";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Archived = "Archived";
    /// <summary>Alias of Draft after engine create (backward compatible).</summary>
    public const string Generated = "Generated";
    /// <summary>Alias of Simulated after simulation accept (backward compatible).</summary>
    public const string SimulationAccepted = "SimulationAccepted";

    public static IReadOnlyList<string> All { get; } =
    [
        Draft, Saved, Simulated, Compared, Reviewed, Approved, Rejected, Archived, Generated, SimulationAccepted
    ];

    public static string Normalize(string? status) => status switch
    {
        Generated => Draft,
        SimulationAccepted => Simulated,
        null or "" => Draft,
        _ => status,
    };
}

/// <summary>AI29.1C.5A — Execution/result status (distinct from LifecycleStatus).</summary>
public static class AllocationExecutionStatus
{
    public const string Generated = "Generated";
    public const string Completed = "Completed";
    public const string CompletedWithErrors = "CompletedWithErrors";
    public const string Failed = "Failed";
    public const string Running = "Running";
    public const string Cancelled = "Cancelled";
    public const string TimedOut = "TimedOut";
    public const string Accepted = "Accepted";

    public static IReadOnlyList<string> All { get; } =
    [
        Generated, Completed, CompletedWithErrors, Failed, Running, Cancelled, TimedOut, Accepted
    ];

    public static bool IsSuccessful(string? status) =>
        status is Completed or Accepted or Generated;

    public static bool IsFailed(string? status) =>
        status is Failed or CompletedWithErrors;
}

public static class AllocationAuditActions
{
    public const string Run = "Run";
    public const string CreateScenario = "CreateScenario";
    public const string Save = "Save";
    public const string Simulate = "Simulate";
    public const string Replay = "Replay";
    public const string Compare = "Compare";
    public const string Review = "Review";
    public const string Approve = "Approve";
    public const string Reject = "Reject";
    public const string Archive = "Archive";
}

/// <summary>AI29.1C.5A — Concurrency conflict user message.</summary>
public static class AllocationConcurrencyMessages
{
    public const string ScenarioChanged =
        "This allocation scenario has changed since you opened it. Refresh the scenario before continuing.";
}
