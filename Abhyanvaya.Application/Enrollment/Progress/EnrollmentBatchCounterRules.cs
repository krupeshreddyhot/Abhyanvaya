using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Enrollment.Progress;

/// <summary>
/// Maps <see cref="EnrollmentStatus"/> values to denormalized batch counter columns.
/// <see cref="EnrollmentStatus.Downloaded"/> shares the downloading bucket until validation starts.
/// </summary>
public static class EnrollmentBatchCounterRules
{
    public static void ApplyTransition(
        StudentEnrollmentBatch batch,
        EnrollmentStatus from,
        EnrollmentStatus to)
    {
        if (from == to)
        {
            return;
        }

        Decrement(batch, from);
        Increment(batch, to);
    }

    public static void Increment(StudentEnrollmentBatch batch, EnrollmentStatus status)
    {
        switch (status)
        {
            case EnrollmentStatus.Pending:
                batch.PendingCount++;
                break;
            case EnrollmentStatus.Downloading:
            case EnrollmentStatus.Downloaded:
                batch.DownloadingCount++;
                break;
            case EnrollmentStatus.Validating:
                batch.ValidatingCount++;
                break;
            case EnrollmentStatus.Embedding:
                batch.EmbeddingCount++;
                break;
            case EnrollmentStatus.Completed:
                batch.CompletedCount++;
                break;
            case EnrollmentStatus.Failed:
                batch.FailedCount++;
                break;
            case EnrollmentStatus.RetryRequired:
                batch.RetryRequiredCount++;
                break;
            case EnrollmentStatus.Cancelled:
                batch.CancelledCount++;
                break;
        }
    }

    public static void Decrement(StudentEnrollmentBatch batch, EnrollmentStatus status)
    {
        switch (status)
        {
            case EnrollmentStatus.Pending:
                batch.PendingCount = Math.Max(0, batch.PendingCount - 1);
                break;
            case EnrollmentStatus.Downloading:
            case EnrollmentStatus.Downloaded:
                batch.DownloadingCount = Math.Max(0, batch.DownloadingCount - 1);
                break;
            case EnrollmentStatus.Validating:
                batch.ValidatingCount = Math.Max(0, batch.ValidatingCount - 1);
                break;
            case EnrollmentStatus.Embedding:
                batch.EmbeddingCount = Math.Max(0, batch.EmbeddingCount - 1);
                break;
            case EnrollmentStatus.Completed:
                batch.CompletedCount = Math.Max(0, batch.CompletedCount - 1);
                break;
            case EnrollmentStatus.Failed:
                batch.FailedCount = Math.Max(0, batch.FailedCount - 1);
                break;
            case EnrollmentStatus.RetryRequired:
                batch.RetryRequiredCount = Math.Max(0, batch.RetryRequiredCount - 1);
                break;
            case EnrollmentStatus.Cancelled:
                batch.CancelledCount = Math.Max(0, batch.CancelledCount - 1);
                break;
        }
    }
}
