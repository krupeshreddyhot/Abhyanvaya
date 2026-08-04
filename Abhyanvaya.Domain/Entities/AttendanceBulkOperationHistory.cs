using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// AI22.8.6.4 — administrator bulk assist audit. Does not finalize attendance or create sessions.
/// </summary>
public class AttendanceBulkOperationHistory : ITenantScoped
{
    public Guid Id { get; set; }
    public int TenantId { get; set; }
    /// <summary>NotifyFaculty | ArchiveExpired | ExportSessions | RetryFailedRecognition | MarkReviewed | CloseCompleted</summary>
    public string Operation { get; set; } = "";
    public int RequestedCount { get; set; }
    public int SucceededCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public string? SessionIdsJson { get; set; }
    public string? ResultJson { get; set; }
    public string? Reason { get; set; }
    public int PerformedBy { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}
