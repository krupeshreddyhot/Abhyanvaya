using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// AI22.8.5.3 — per-faculty attendance recovery preferences (tenant-scoped).
/// Separate from scheduling WorkspacePreference; does not change Attendance APIs.
/// </summary>
public class AttendanceRecoveryPreference : BaseEntity
{
    public int StaffId { get; set; }
    public int UserId { get; set; }

    /// <summary>Checkpoint auto-save interval in seconds (UI guidance).</summary>
    public int AutoSaveFrequencySeconds { get; set; } = 30;

    public bool ResumeConfirmation { get; set; } = true;

    /// <summary>pending | recovery | home</summary>
    public string DefaultLandingPage { get; set; } = "pending";

    public bool NotificationsEnabled { get; set; } = true;
    public bool SessionTimeoutWarning { get; set; } = true;
    public int SessionTimeoutWarningMinutes { get; set; } = 30;
    public bool PromptOnLogin { get; set; } = true;
}
