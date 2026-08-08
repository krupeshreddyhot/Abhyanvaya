using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.1B — Tenant-level capacity rules (warnings only; no auto student movement).</summary>
public class TenantSectionCapacityPolicy : BaseEntity
{
    public int CollegeId { get; set; }

    /// <summary>When true, assignments may not exceed Maximum Capacity.</summary>
    public bool EnforceHardLimit { get; set; } = true;

    /// <summary>When true, soft over-capacity produces warnings only.</summary>
    public bool SoftLimitEnabled { get; set; } = true;

    /// <summary>Occupancy % at/above which a warning is raised (default 90).</summary>
    public int WarningPercent { get; set; } = 90;

    public bool AutoWarningEnabled { get; set; } = true;

    /// <summary>Occupancy % at/below which under-capacity warning is raised (default 40).</summary>
    public int UnderCapacityPercent { get; set; } = 40;
}
