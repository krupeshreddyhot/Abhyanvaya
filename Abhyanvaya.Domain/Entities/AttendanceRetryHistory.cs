using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// AI22.8 — stage-aware retry audit trail. Reuses session identity; does not create sessions.
/// </summary>
public class AttendanceRetryHistory : ITenantScoped
{
    public Guid Id { get; set; }
    public int TenantId { get; set; }
    public Guid AttendanceSessionId { get; set; }
    /// <summary>Recognition | FailedImages | Upload | Finalization | EntireSession</summary>
    public string Stage { get; set; } = "";
    /// <summary>RetryRecognition | RetryFailedImages | RetryUpload | RetryFinalization | RetryEntireSession</summary>
    public string Action { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public AttendanceWorkflowStatus? WorkflowStatusBefore { get; set; }
    public AttendanceWorkflowStatus? WorkflowStatusAfter { get; set; }
    public int PerformedBy { get; set; }
    public DateTime PerformedUtc { get; set; }
    public AttendanceSession? AttendanceSession { get; set; }
}
