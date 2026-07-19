using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Enrollment.Background;

public sealed record EnrollmentLease
{
    public required Guid LeaseId { get; init; }
    public required string WorkerId { get; init; }
    public required string NodeId { get; init; }
    public required Guid ItemId { get; init; }
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required int StudentId { get; init; }
    public required DateTime AcquiredUtc { get; init; }
    public required DateTime ExpiresUtc { get; init; }
    public required DateTime HeartbeatUtc { get; init; }
    public required int RenewalCount { get; init; }
    public required Guid CorrelationId { get; init; }
    public required byte[] LeaseVersion { get; init; }
    public EnrollmentWorkerState PipelineState { get; init; } = EnrollmentWorkerState.Running;
}

public sealed record EnrollmentWorkItem
{
    public required Guid ItemId { get; init; }
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required int StudentId { get; init; }
    public required EnrollmentStatus Status { get; init; }
    public required int PipelineVersion { get; init; }
    public required Guid CorrelationId { get; init; }
    public required string PhotoProviderName { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public required string StudentNumber { get; init; }
    public required string CollegeCode { get; init; }
    public string? SourceUrl { get; init; }
    public string? ContentType { get; init; }
    public int? ByteSize { get; init; }
    public int RetryCount { get; init; }
    public DateTime? NextAttemptUtc { get; init; }
    public int BatchPriority { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed record EnrollmentWorkerResult
{
    public required Guid EnrollmentId { get; init; }
    public required string WorkerId { get; init; }
    public required Guid LeaseId { get; init; }
    public required TimeSpan Duration { get; init; }
    public EnrollmentPipelineResult? PipelineResult { get; init; }
    public int Retries { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public EnrollmentWorkerStatistics? Statistics { get; init; }
    public bool Success { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record EnrollmentWorkerStatistics
{
    public int ItemsProcessed { get; init; }
    public TimeSpan LeaseDuration { get; init; }
    public int RetryCount { get; init; }
    public int RecoveryCount { get; init; }
    public int FailureCount { get; init; }
    public double? AverageThroughputPerMinute { get; init; }
    public TimeSpan? AveragePipelineDuration { get; init; }
}

public sealed record EnrollmentRetryScheduleRequest
{
    public required EnrollmentWorkItem WorkItem { get; init; }
    public required string StageName { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public int AttemptCount { get; init; }
}

public sealed record EnrollmentRetryScheduleResult
{
    public required bool Scheduled { get; init; }
    public DateTime? NextAttemptUtc { get; init; }
    public bool MovedToDeadLetter { get; init; }
    public string? Reason { get; init; }
}

public sealed record EnrollmentDeadLetterRequest
{
    public required EnrollmentWorkItem WorkItem { get; init; }
    public required string FailureReason { get; init; }
    public string? FailureCode { get; init; }
    public string? ExceptionSummary { get; init; }
    public IReadOnlyList<string>? RetryHistory { get; init; }
}

public sealed record EnrollmentRecoveryResult
{
    public required int ExpiredLeasesRecovered { get; init; }
    public required int StuckItemsRecovered { get; init; }
    public required int RequeuedItems { get; init; }
    public required TimeSpan Duration { get; init; }
}
