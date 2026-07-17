using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Sole writer of enrollment item transitions, batch counters, progress metrics, and snapshots
/// (docs/AI20_PHASE2_ENGINE_CONTRACTS.md §5).
/// </summary>
public interface IEnrollmentProgressReporter
{
    Task<EnrollmentTransitionResult> TransitionItemAsync(
        EnrollmentTransitionRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTransitionResult> MarkItemStartedAsync(
        EnrollmentProgressOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTransitionResult> MarkStageCompletedAsync(
        EnrollmentStageProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTransitionResult> MarkStageFailedAsync(
        EnrollmentStageProgressRequest request,
        EnrollmentStatus terminalStatus,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTransitionResult> MarkRetryScheduledAsync(
        EnrollmentRetryProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTransitionResult> MarkItemCompletedAsync(
        EnrollmentProgressOperationRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTransitionResult> MarkBatchCompletedAsync(
        EnrollmentBatchProgressRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentTransitionResult> MarkBatchFailedAsync(
        EnrollmentBatchProgressRequest request,
        CancellationToken cancellationToken = default);

    Task FinalizeBatchIfCompleteAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentProgressDetail?> UpdateProgressAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default);

    EnrollmentProgressMetrics CalculateProgress(
        StudentEnrollmentBatchCounters counters,
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions,
        int uploadingItems,
        DateTime utcNow);

    EnrollmentEtaResult CalculateETA(
        int remainingItems,
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions,
        DateTime utcNow);

    Task<EnrollmentProgressSnapshotRecord?> PersistProgressSnapshotAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentProgress?> GetProgressAsync(
        Guid batchId,
        int tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>O(1) batch counter inputs for progress calculation without entity leakage.</summary>
public sealed record StudentEnrollmentBatchCounters
{
    public required int TotalStudents { get; init; }
    public required int PendingCount { get; init; }
    public required int DownloadingCount { get; init; }
    public required int ValidatingCount { get; init; }
    public required int EmbeddingCount { get; init; }
    public required int CompletedCount { get; init; }
    public required int FailedCount { get; init; }
    public required int RetryRequiredCount { get; init; }
    public required int CancelledCount { get; init; }
}

public sealed record RecentEnrollmentCompletionSample
{
    public required DateTime CreatedUtc { get; init; }
    public required DateTime CompletedUtc { get; init; }
    public DateTime? DownloadStartedUtc { get; init; }
    public DateTime? DownloadedUtc { get; init; }
}
