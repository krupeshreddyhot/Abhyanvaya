namespace Abhyanvaya.Domain.ValueObjects;

/// <summary>
/// Immutable snapshot of per-status item counts for one <see cref="Entities.StudentEnrollmentBatch"/>.
/// Mirrors the denormalized counter columns on the batch row (docs/AI20_ENROLLMENT_DATABASE.md §3.1)
/// so callers get a single, self-consistent read without recomputing aggregates from
/// <see cref="Entities.StudentEnrollmentItem"/> rows on every dashboard poll.
/// <para>
/// Zero dependency on EF Core or any infrastructure concern — pure domain arithmetic only.
/// </para>
/// </summary>
public sealed class EnrollmentStatistics
{
    public int Total { get; }

    public int Pending { get; }

    public int Downloading { get; }

    public int Validating { get; }

    public int Embedding { get; }

    public int Completed { get; }

    public int Failed { get; }

    public int RetryRequired { get; }

    public int Cancelled { get; }

    public EnrollmentStatistics(
        int total,
        int pending,
        int downloading,
        int validating,
        int embedding,
        int completed,
        int failed,
        int retryRequired,
        int cancelled)
    {
        Total = total;
        Pending = pending;
        Downloading = downloading;
        Validating = validating;
        Embedding = embedding;
        Completed = completed;
        Failed = failed;
        RetryRequired = retryRequired;
        Cancelled = cancelled;
    }

    /// <summary>Items that will never be picked up again without explicit SuperAdmin action.</summary>
    public int TerminalCount => Completed + Failed + Cancelled;

    /// <summary>Items still moving through the pipeline (including those awaiting an automatic retry).</summary>
    public int InFlightCount => Pending + Downloading + Validating + Embedding + RetryRequired;

    /// <summary>Percentage of items that have reached a terminal state (0–100).</summary>
    public decimal CompletionPercentage =>
        Total <= 0 ? 0m : Math.Round(TerminalCount * 100m / Total, 1);

    /// <summary>Percentage of terminal items that succeeded (0–100); 0 when nothing has finished yet.</summary>
    public decimal SuccessRatePercentage =>
        TerminalCount <= 0 ? 0m : Math.Round(Completed * 100m / TerminalCount, 1);

    public static EnrollmentStatistics Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}
