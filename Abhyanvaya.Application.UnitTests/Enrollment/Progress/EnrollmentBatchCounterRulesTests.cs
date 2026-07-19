using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Progress;

public sealed class EnrollmentBatchCounterRulesTests
{
    [Fact]
    public void ApplyTransition_MovesCountersFromPendingToDownloading()
    {
        var batch = CreateBatch(pending: 5, downloading: 0);

        EnrollmentBatchCounterRules.ApplyTransition(
            batch,
            EnrollmentStatus.Pending,
            EnrollmentStatus.Downloading);

        Assert.Equal(4, batch.PendingCount);
        Assert.Equal(1, batch.DownloadingCount);
    }

    [Fact]
    public void ApplyTransition_DownloadedToValidating_AdjustsDownloadingBucket()
    {
        var batch = CreateBatch(downloading: 3, validating: 0);

        EnrollmentBatchCounterRules.ApplyTransition(
            batch,
            EnrollmentStatus.Downloaded,
            EnrollmentStatus.Validating);

        Assert.Equal(2, batch.DownloadingCount);
        Assert.Equal(1, batch.ValidatingCount);
    }

    [Fact]
    public void ApplyTransition_DownloadingToDownloaded_DoesNotChangeCounters()
    {
        var batch = CreateBatch(downloading: 2);

        EnrollmentBatchCounterRules.ApplyTransition(
            batch,
            EnrollmentStatus.Downloading,
            EnrollmentStatus.Downloaded);

        Assert.Equal(2, batch.DownloadingCount);
    }

    private static StudentEnrollmentBatch CreateBatch(
        int pending = 0,
        int downloading = 0,
        int validating = 0,
        int embedding = 0,
        int completed = 0,
        int failed = 0,
        int retry = 0,
        int cancelled = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = 1,
            UniversityId = 1,
            CollegeId = 1,
            AcademicYear = 2026,
            PhotoProviderName = "ExamBranch",
            TotalStudents = pending + downloading + validating + embedding + completed + failed + retry + cancelled,
            PendingCount = pending,
            DownloadingCount = downloading,
            ValidatingCount = validating,
            EmbeddingCount = embedding,
            CompletedCount = completed,
            FailedCount = failed,
            RetryRequiredCount = retry,
            CancelledCount = cancelled,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
}
