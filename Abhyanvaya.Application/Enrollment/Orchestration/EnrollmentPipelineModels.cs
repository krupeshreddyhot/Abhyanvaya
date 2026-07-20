using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Enrollment.Orchestration;

public static class EnrollmentPipelineFailureCodes
{
    public const string StageFailed = "pipeline.stage_failed";
    public const string Cancelled = "pipeline.cancelled";
    public const string ProgressConflict = "pipeline.progress_conflict";
    public const string UnexpectedFailure = "pipeline.unexpected_failure";
}

public enum EnrollmentPipelineState
{
    Pending = 0,
    Validating = 1,
    Validated = 2,
    Storing = 3,
    Stored = 4,
    Embedding = 5,
    Embedded = 6,
    Persisting = 7,
    Persisted = 8,
    Completed = 9,
    Failed = 10,
    Cancelled = 11,
}

public enum EnrollmentStageOutcome
{
    Succeeded = 0,
    Failed = 1,
    RetryRequired = 2,
    Cancelled = 3,
}

public sealed record EnrollmentStageResult
{
    public required EnrollmentStageOutcome Outcome { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? Reason { get; init; }

    public static EnrollmentStageResult Ok() =>
        new() { Outcome = EnrollmentStageOutcome.Succeeded };

    public static EnrollmentStageResult Fail(FailureCategory category, string reason) =>
        new() { Outcome = EnrollmentStageOutcome.Failed, FailureCategory = category, Reason = reason };

    public static EnrollmentStageResult Retry(FailureCategory? category, string reason) =>
        new() { Outcome = EnrollmentStageOutcome.RetryRequired, FailureCategory = category, Reason = reason };

    public static EnrollmentStageResult Cancelled(string reason) =>
        new() { Outcome = EnrollmentStageOutcome.Cancelled, Reason = reason };
}

public sealed record EnrollmentItemContext
{
    public required Guid BatchId { get; init; }
    public required Guid ItemId { get; init; }
    public required int TenantId { get; init; }
    public required int StudentId { get; init; }
    public required string StudentNumber { get; init; }
    public required string CollegeCode { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public required string PhotoProviderName { get; init; }
    public required Guid ExecutionTraceId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required int PipelineVersion { get; init; }
    public EnrollmentPipelineStage? CurrentStage { get; init; }
    public EnrollmentPipelineState PipelineState { get; init; } = EnrollmentPipelineState.Pending;
    public bool CancellationRequested { get; init; }
}

public sealed record EnrollmentPipelineRequest
{
    public required EnrollmentItemContext Context { get; init; }
    public required EnrollmentStatus ItemStatus { get; init; }
    public string? SourceUrl { get; init; }
    public string? ContentType { get; init; }
    public int? ByteSize { get; init; }
    public byte[]? PhotoBytes { get; init; }
}

public sealed record EnrollmentPipelineStageOutcome
{
    public required EnrollmentPipelineStage? ManifestStage { get; init; }
    public required string StageName { get; init; }
    public required bool Success { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public int RetryAttempts { get; init; }
}

public sealed record EnrollmentPipelineStatistics
{
    public TimeSpan TotalDuration { get; init; }
    public IReadOnlyDictionary<string, TimeSpan>? StageDurations { get; init; }
    public int RetryCount { get; init; }
    public int FailureCount { get; init; }
    public bool WasCancelled { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
}

public sealed record EnrollmentPipelineResult
{
    public required bool Success { get; init; }
    public required int StudentId { get; init; }
    public required Guid BatchId { get; init; }
    public required Guid ItemId { get; init; }
    public EnrollmentPipelineStage? CompletedStage { get; init; }
    public EnrollmentPipelineState Status { get; init; }
    public EnrollmentStatus? ItemStatus { get; init; }
    public required TimeSpan Duration { get; init; }
    public required Guid CorrelationId { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public EnrollmentPipelineStatistics? Statistics { get; init; }
    public IReadOnlyList<EnrollmentPipelineStageOutcome>? StageResults { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public EnrollmentPersistenceResult? PersistenceResult { get; init; }

    public static EnrollmentPipelineResult Succeeded(
        EnrollmentPipelineRequest request,
        EnrollmentPipelineState status,
        EnrollmentPipelineStage? completedStage,
        EnrollmentStatus itemStatus,
        TimeSpan duration,
        IReadOnlyList<EnrollmentPipelineStageOutcome> stageResults,
        EnrollmentPipelineStatistics statistics,
        IReadOnlyList<string>? warnings,
        EnrollmentPersistenceResult? persistenceResult = null) =>
        new()
        {
            Success = true,
            StudentId = request.Context.StudentId,
            BatchId = request.Context.BatchId,
            ItemId = request.Context.ItemId,
            CompletedStage = completedStage,
            Status = status,
            ItemStatus = itemStatus,
            Duration = duration,
            CorrelationId = request.Context.CorrelationId,
            Warnings = warnings,
            Statistics = statistics,
            StageResults = stageResults,
            PersistenceResult = persistenceResult,
        };

    public static EnrollmentPipelineResult Failed(
        EnrollmentPipelineRequest request,
        EnrollmentPipelineStage? failedStage,
        EnrollmentPipelineState status,
        TimeSpan duration,
        IReadOnlyList<EnrollmentPipelineStageOutcome> stageResults,
        EnrollmentPipelineStatistics statistics,
        string failureCode,
        string failureReason,
        FailureCategory? failureCategory = null) =>
        new()
        {
            Success = false,
            StudentId = request.Context.StudentId,
            BatchId = request.Context.BatchId,
            ItemId = request.Context.ItemId,
            CompletedStage = failedStage,
            Status = status,
            Duration = duration,
            CorrelationId = request.Context.CorrelationId,
            Statistics = statistics,
            StageResults = stageResults,
            FailureCode = failureCode,
            FailureReason = failureReason,
            FailureCategory = failureCategory,
        };
}

public sealed record EnrollmentPipelineStageExecutionResult
{
    public required bool Success { get; init; }
    public required EnrollmentPipelineContext Context { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public bool IsRetryable { get; init; }
    public bool IsCancelled { get; init; }
    public required TimeSpan Duration { get; init; }
    public int RetryAttempts { get; init; }

    public static EnrollmentPipelineStageExecutionResult Succeeded(
        EnrollmentPipelineContext context,
        TimeSpan duration,
        int retryAttempts = 0) =>
        new()
        {
            Success = true,
            Context = context,
            Duration = duration,
            RetryAttempts = retryAttempts,
        };

    public static EnrollmentPipelineStageExecutionResult Failed(
        EnrollmentPipelineContext context,
        TimeSpan duration,
        string failureCode,
        string failureReason,
        FailureCategory? failureCategory = null,
        bool isRetryable = false,
        int retryAttempts = 0) =>
        new()
        {
            Success = false,
            Context = context,
            Duration = duration,
            FailureCode = failureCode,
            FailureReason = failureReason,
            FailureCategory = failureCategory,
            IsRetryable = isRetryable,
            RetryAttempts = retryAttempts,
        };

    public static EnrollmentPipelineStageExecutionResult Cancelled(
        EnrollmentPipelineContext context,
        TimeSpan duration) =>
        new()
        {
            Success = false,
            Context = context,
            Duration = duration,
            IsCancelled = true,
            FailureCode = EnrollmentPipelineFailureCodes.Cancelled,
            FailureReason = "Pipeline execution was cancelled.",
        };
}

public sealed record EnrollmentPipelineContext
{
    public required EnrollmentPipelineRequest Request { get; init; }
    public required EnrollmentPipelineState State { get; init; }
    public EnrollmentPipelineStage? CurrentStage { get; init; }
    public byte[]? PhotoBytes { get; init; }
    public string? ContentType { get; init; }
    public long? PhotoByteSize { get; init; }
    public EnrollmentValidationResult? ValidationResult { get; init; }
    public EnrollmentValidationArtifact? ValidationArtifact { get; init; }
    public EnrollmentStorageResult? StorageResult { get; init; }
    public EnrollmentStorageManifest? StorageManifest { get; init; }
    public EnrollmentEmbeddingResult? EmbeddingResult { get; init; }
    public EnrollmentEmbeddingArtifact? EmbeddingArtifact { get; init; }
    public EnrollmentPersistenceResult? PersistenceResult { get; init; }
    public bool EmbeddingEligible { get; init; } = true;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int TotalRetryCount { get; init; }

    public EnrollmentItemContext ItemContext => Request.Context;

    public static EnrollmentPipelineContext Create(EnrollmentPipelineRequest request) =>
        new()
        {
            Request = request,
            State = MapInitialState(request.ItemStatus),
            PhotoBytes = request.PhotoBytes,
            ContentType = request.ContentType,
            PhotoByteSize = request.ByteSize ?? request.PhotoBytes?.LongLength,
        };

    private static EnrollmentPipelineState MapInitialState(EnrollmentStatus status) =>
        status switch
        {
            EnrollmentStatus.Downloaded => EnrollmentPipelineState.Pending,
            EnrollmentStatus.Validating => EnrollmentPipelineState.Validating,
            EnrollmentStatus.Embedding => EnrollmentPipelineState.Embedding,
            EnrollmentStatus.Downloading => EnrollmentPipelineState.Pending,
            EnrollmentStatus.Pending or EnrollmentStatus.RetryRequired => EnrollmentPipelineState.Pending,
            EnrollmentStatus.Completed => EnrollmentPipelineState.Completed,
            EnrollmentStatus.Failed => EnrollmentPipelineState.Failed,
            EnrollmentStatus.Cancelled => EnrollmentPipelineState.Cancelled,
            _ => EnrollmentPipelineState.Pending,
        };
}

public sealed record EnrollmentRetryDecision
{
    public required bool ShouldRetry { get; init; }
    public TimeSpan Delay { get; init; }
    public string? Reason { get; init; }

    public static EnrollmentRetryDecision NoRetry(string? reason = null) =>
        new() { ShouldRetry = false, Reason = reason };

    public static EnrollmentRetryDecision Retry(TimeSpan delay, string? reason = null) =>
        new() { ShouldRetry = true, Delay = delay, Reason = reason };
}
