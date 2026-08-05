using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Dashboards;

/// <summary>
/// AI31.6.9 — per-user, per-tenant dashboard personalization (DB-persisted, not localStorage-only).
/// RoleScope: Faculty | Admin | Principal | SuperAdmin (future roles share same store).
/// </summary>
public class DashboardPreference : BaseEntity
{
    public int UserId { get; set; }
    /// <summary>Faculty | Admin | Principal | SuperAdmin | AcademicAdmin</summary>
    public string RoleScope { get; set; } = "Faculty";
    /// <summary>command-center | faculty-workspace | admin-operations | analytics | health | notifications</summary>
    public string DefaultLandingPage { get; set; } = "command-center";
    public bool CompactMode { get; set; }
    /// <summary>JSON array of hidden widget codes.</summary>
    public string HiddenWidgetsJson { get; set; } = "[]";
    /// <summary>JSON array of widget codes in display order.</summary>
    public string WidgetOrderJson { get; set; } = "[]";
    /// <summary>AI31.8 — JSON array of pinned widget codes (per user + tenant).</summary>
    public string PinnedWidgetsJson { get; set; } = "[]";
    /// <summary>AI31.8 — persisted global filter selections JSON.</summary>
    public string FilterJson { get; set; } = "{}";
    /// <summary>AI31.8 — auto-refresh interval seconds (30/60/120/300; 0 = manual).</summary>
    public int RefreshIntervalSeconds { get; set; } = 60;
    /// <summary>AI31.8 — high-contrast accessibility preference.</summary>
    public bool HighContrast { get; set; }
}
