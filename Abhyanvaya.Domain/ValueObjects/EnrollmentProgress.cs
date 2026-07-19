using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.ValueObjects;

/// <summary>
/// Immutable read-model combining a batch's <see cref="BatchStatus"/>, its <see cref="EnrollmentStatistics"/>,
/// and its lifecycle timestamps into the single value the SuperAdmin Progress Screen needs
/// (docs/AI20_ENROLLMENT_UI.md §3). Pure domain composition — no persistence or query logic.
/// </summary>
public sealed class EnrollmentProgress
{
    public BatchStatus Status { get; }

    public EnrollmentStatistics Statistics { get; }

    public DateTime? StartedUtc { get; }

    public DateTime? CompletedUtc { get; }

    public DateTime? CancellationRequestedUtc { get; }

    public EnrollmentProgress(
        BatchStatus status,
        EnrollmentStatistics statistics,
        DateTime? startedUtc,
        DateTime? completedUtc,
        DateTime? cancellationRequestedUtc)
    {
        Status = status;
        Statistics = statistics;
        StartedUtc = startedUtc;
        CompletedUtc = completedUtc;
        CancellationRequestedUtc = cancellationRequestedUtc;
    }

    /// <summary>True once the batch will never process another item automatically.</summary>
    public bool IsTerminal =>
        Status is BatchStatus.Completed or BatchStatus.PartiallyFailed or BatchStatus.Cancelled;

    /// <summary>True when a SuperAdmin has asked this batch to stop claiming new items.</summary>
    public bool IsCancellationRequested => CancellationRequestedUtc.HasValue;

    /// <summary>Wall-clock time elapsed since the batch started, or since it started until it completed.</summary>
    public TimeSpan? Elapsed => StartedUtc is null ? null : (CompletedUtc ?? DateTime.UtcNow) - StartedUtc.Value;

    /// <summary>Convenience accessor mirroring <see cref="EnrollmentStatistics.CompletionPercentage"/>.</summary>
    public decimal CompletionPercentage => Statistics.CompletionPercentage;
}
