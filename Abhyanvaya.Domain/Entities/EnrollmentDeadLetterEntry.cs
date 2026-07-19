namespace Abhyanvaya.Domain.Entities;

/// <summary>Permanently failed enrollment work item for manual review/replay (AI20.PHASE2.2).</summary>
public class EnrollmentDeadLetterEntry
{
    public Guid Id { get; set; }

    public Guid ItemId { get; set; }

    public Guid BatchId { get; set; }

    public int TenantId { get; set; }

    public int StudentId { get; set; }

    public required string FailureReason { get; set; }

    public string? FailureCode { get; set; }

    public string? ExceptionSummary { get; set; }

    public int RetryCount { get; set; }

    public Guid CorrelationId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string? RetryHistoryJson { get; set; }
}
