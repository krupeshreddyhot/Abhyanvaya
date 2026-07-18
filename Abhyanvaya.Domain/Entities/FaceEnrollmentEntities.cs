using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

public class FaceEnrollmentJob
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public Guid AcquisitionItemId { get; set; }

    public Guid AcquisitionBatchId { get; set; }

    public int TenantId { get; set; }

    public int StudentId { get; set; }

    public required string StudentNumber { get; set; }

    public EnrollmentState State { get; set; } = EnrollmentState.Queued;

    public Guid CorrelationId { get; set; }

    public Guid TraceId { get; set; }

    public string? ArtifactJson { get; set; }

    public string? FailureReason { get; set; }

    public int RetryCount { get; set; }

    public decimal? QualityScore { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public DateTime? LastStateChangeUtc { get; set; }
}

public class FaceEnrollmentBatch
{
    public Guid Id { get; set; }

    public Guid AcquisitionBatchId { get; set; }

    public int TenantId { get; set; }

    public EnrollmentState State { get; set; } = EnrollmentState.Queued;

    public int TotalItems { get; set; }

    public int CompletedCount { get; set; }

    public int FailedCount { get; set; }

    public int DuplicateCount { get; set; }

    public int RetryCount { get; set; }

    public string? ManifestJson { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public ICollection<FaceEnrollmentJob> Jobs { get; set; } = new List<FaceEnrollmentJob>();
}
