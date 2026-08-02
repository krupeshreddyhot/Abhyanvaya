using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Scheduling;

/// <summary>Tenant-scoped configurable conflict thresholds (AI30 Phase 2B.5). Detection rules unchanged; values are configurable.</summary>
public class ConflictRuleThresholdSetting : BaseEntity
{
    public string ThresholdKey { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Value { get; set; }
    public string Unit { get; set; } = "count";
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class ConflictRuleConfigChangeHistory : BaseEntity
{
    public string ThresholdKey { get; set; } = null!;
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public int Version { get; set; }
    public string? ChangeReason { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTime ChangedUtc { get; set; }
}

public class ConflictWorkspacePin : BaseEntity
{
    public int ConflictDetectionRunId { get; set; }
    public string RuleCode { get; set; } = null!;
    public int? TimetableEntryId { get; set; }
    public int UserId { get; set; }
}

public class ConflictWorkspaceBookmark : BaseEntity
{
    public string Name { get; set; } = null!;
    public string FilterJson { get; set; } = "{}";
    public int UserId { get; set; }
}

public class ConflictWorkspaceNote : BaseEntity
{
    public int ConflictDetectionRunId { get; set; }
    public string RuleCode { get; set; } = null!;
    public int? TimetableEntryId { get; set; }
    public string NoteText { get; set; } = null!;
    public int UserId { get; set; }
}
