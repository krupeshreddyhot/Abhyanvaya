using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;

namespace Abhyanvaya.Application.Enrollment.Progress;

public sealed record EnrollmentTransitionRequest
{
    public required Guid ItemId { get; init; }
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required EnrollmentStatus FromStatus { get; init; }
    public required EnrollmentStatus ToStatus { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? LastError { get; init; }
    public EnrollmentStageTimestamp? StampTimestamp { get; init; }
    public Guid? CorrelationId { get; init; }
    public Guid? ExecutionTraceId { get; init; }
    public int? PipelineVersion { get; init; }
}

public enum EnrollmentStageTimestamp
{
    DownloadStarted,
    Downloaded,
    ValidationStarted,
    Validated,
    EmbeddingStarted,
    Completed,
}

public sealed record EnrollmentTransitionResult
{
    public required bool Applied { get; init; }
    public required bool ConcurrencyConflict { get; init; }
    public string? Reason { get; init; }

    public static EnrollmentTransitionResult AppliedOk() =>
        new() { Applied = true, ConcurrencyConflict = false };

    public static EnrollmentTransitionResult NotApplied(string reason) =>
        new() { Applied = false, ConcurrencyConflict = false, Reason = reason };

    public static EnrollmentTransitionResult Conflict() =>
        new() { Applied = false, ConcurrencyConflict = true, Reason = "RowVersion conflict." };
}

public sealed record EnrollmentProgressOperationRequest
{
    public required Guid ItemId { get; init; }
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required EnrollmentStatus ExpectedStatus { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? LastError { get; init; }
    public Guid? CorrelationId { get; init; }
    public Guid? ExecutionTraceId { get; init; }
    public int? PipelineVersion { get; init; }
}

public sealed record EnrollmentStageProgressRequest
{
    public required Guid ItemId { get; init; }
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required EnrollmentStatus ExpectedStatus { get; init; }
    public required EnrollmentPipelineStage Stage { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? LastError { get; init; }
    public Guid? CorrelationId { get; init; }
    public Guid? ExecutionTraceId { get; init; }
    public int? PipelineVersion { get; init; }
}

public sealed record EnrollmentRetryProgressRequest
{
    public required Guid ItemId { get; init; }
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required EnrollmentStatus ExpectedStatus { get; init; }
    public required EnrollmentStatus FromStatus { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? LastError { get; init; }
    public Guid? CorrelationId { get; init; }
    public Guid? ExecutionTraceId { get; init; }
    public int? PipelineVersion { get; init; }
}

public sealed record EnrollmentBatchProgressRequest
{
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public Guid? CorrelationId { get; init; }
    public int? PipelineVersion { get; init; }
}

public sealed record EnrollmentProgressMetrics
{
    public required int TotalItems { get; init; }
    public required int PendingItems { get; init; }
    public required int DownloadingItems { get; init; }
    public required int ValidatingItems { get; init; }
    public required int UploadingItems { get; init; }
    public required int EmbeddingItems { get; init; }
    public required int CompletedItems { get; init; }
    public required int RetryItems { get; init; }
    public required int FailedItems { get; init; }
    public required int CancelledItems { get; init; }
    public required decimal CompletionPercentage { get; init; }
    public double? AverageItemDurationSeconds { get; init; }
    public double? AverageStageDurationSeconds { get; init; }
    public double? ItemsPerMinute { get; init; }
    public required int RemainingItems { get; init; }
    public DateTime? EstimatedCompletionUtc { get; init; }
    public required bool EtaIsKnown { get; init; }
}

public sealed record EnrollmentEtaResult
{
    public required bool IsKnown { get; init; }
    public DateTime? EstimatedCompletionUtc { get; init; }

    public static EnrollmentEtaResult Unknown() => new() { IsKnown = false };

    public static EnrollmentEtaResult Known(DateTime estimatedCompletionUtc) =>
        new() { IsKnown = true, EstimatedCompletionUtc = estimatedCompletionUtc };
}

public sealed record EnrollmentProgressDetail
{
    public required EnrollmentProgress Progress { get; init; }
    public required EnrollmentProgressMetrics Metrics { get; init; }
    public required byte[] RowVersion { get; init; }
}

public sealed record EnrollmentProgressSnapshotRecord
{
    public required Guid SnapshotId { get; init; }
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required DateTime CapturedUtc { get; init; }
    public required int Completed { get; init; }
    public required int Pending { get; init; }
    public required int Failed { get; init; }
    public required int Retry { get; init; }
    public required int Downloading { get; init; }
    public required int Validating { get; init; }
    public required int Uploading { get; init; }
    public required int Embedding { get; init; }
    public required int Cancelled { get; init; }
    public required decimal CompletionPercentage { get; init; }
    public DateTime? EstimatedCompletionUtc { get; init; }
    public required bool EtaIsKnown { get; init; }
    public double? ItemsPerMinute { get; init; }
}
