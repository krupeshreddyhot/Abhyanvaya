using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.1C — Persisted allocation session (never commits live student rows).</summary>
public class AllocationEngineSession : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid ContextId { get; set; }
    public string ContextChecksum { get; set; } = "";
    public string Status { get; set; } = "Created";
    public string GroupingMode { get; set; } = "";
    public Guid? ActiveScenarioId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AcademicYearId { get; set; }
    public int CourseId { get; set; }
    public int GroupId { get; set; }
    public int SemesterId { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public string TraceJson { get; set; } = "[]";
}

/// <summary>
/// AI29.1C — Immutable scenario snapshot JSON (lifecycle extended in AI29.1C.5 / hardened in AI29.1C.5A).
/// <see cref="Status"/> = execution/result status.
/// <see cref="LifecycleStatus"/> = governance lifecycle (authoritative).
/// </summary>
public class AllocationEngineScenario : BaseEntity
{
    public Guid ScenarioId { get; set; }
    public Guid SessionId { get; set; }
    public Guid ContextId { get; set; }
    public string ContextChecksum { get; set; } = "";
    /// <summary>Execution/result status (Generated/Completed/Failed/…).</summary>
    public string Status { get; set; } = "Generated";
    public double TotalScore { get; set; }
    public string ScenarioJson { get; set; } = "{}";
    public DateTime GeneratedAt { get; set; }

    // AI29.1C.5 — operations / governance
    public int CurrentVersionNumber { get; set; } = 1;
    /// <summary>Authoritative governance lifecycle (Draft/Saved/…/Archived).</summary>
    public string LifecycleStatus { get; set; } = "Generated";
    public string ContextVersion { get; set; } = "1";
    public string StrategyConfigurationVersion { get; set; } = "1";
    public string ConstraintConfigurationVersion { get; set; } = "1";
    public string ScenarioChecksum { get; set; } = "";
    public int AcademicYearId { get; set; }
    public int CourseId { get; set; }
    public int GroupId { get; set; }
    public int SemesterId { get; set; }
    public Guid? ParentScenarioId { get; set; }
    public string? ReviewNotes { get; set; }

    /// <summary>AI29.1C.5A — Optimistic concurrency token (bytea).</summary>
    public byte[] RowVersion { get; set; } = null!;
}

/// <summary>AI29.1C — Approval creates draft only (no live StudentSection writes).</summary>
public class AllocationEngineDraft : BaseEntity
{
    public Guid DraftId { get; set; }
    public Guid ScenarioId { get; set; }
    public Guid SessionId { get; set; }
    public string Status { get; set; } = "Draft";
    public int? ApprovedBy { get; set; }
    public string DraftJson { get; set; } = "{}";
    public string Note { get; set; } = "Draft only — live student allocations were not modified.";
}

/// <summary>AI29.1C — Sandbox saved scenarios (AI30 philosophy).</summary>
public class AllocationEngineSandboxItem : BaseEntity
{
    public Guid SandboxId { get; set; }
    public string Name { get; set; } = "";
    public Guid ScenarioId { get; set; }
    public Guid SessionId { get; set; }
    public bool IsArchived { get; set; }
    public string? Tags { get; set; }
    public string ScenarioJson { get; set; } = "{}";
}
