using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Progress;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Progress;

public sealed class EnrollmentProgressCalculatorTests
{
    [Fact]
    public void CalculateEta_ReturnsUnknown_WhenInsufficientSamples()
    {
        var eta = EnrollmentProgressCalculator.CalculateEta(
            DateTime.UtcNow,
            remainingItems: 10,
            averageItemDurationSeconds: 30,
            completedSampleCount: 1);

        Assert.False(eta.IsKnown);
    }

    [Fact]
    public void CalculateEta_ReturnsKnown_WhenEnoughHistoryExists()
    {
        var utcNow = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var eta = EnrollmentProgressCalculator.CalculateEta(
            utcNow,
            remainingItems: 4,
            averageItemDurationSeconds: 30,
            completedSampleCount: 3);

        Assert.True(eta.IsKnown);
        Assert.Equal(utcNow.AddSeconds(120), eta.EstimatedCompletionUtc);
    }

    [Fact]
    public void BuildMetrics_CalculatesCompletionPercentage()
    {
        var counters = new StudentEnrollmentBatchCounters
        {
            TotalStudents = 10,
            PendingCount = 2,
            DownloadingCount = 1,
            ValidatingCount = 1,
            EmbeddingCount = 1,
            CompletedCount = 4,
            FailedCount = 1,
            RetryRequiredCount = 0,
            CancelledCount = 0,
        };

        var metrics = EnrollmentProgressCalculator.BuildMetrics(
            counters,
            recentCompletions: [],
            uploadingItems: 1,
            utcNow: DateTime.UtcNow);

        Assert.Equal(50m, metrics.CompletionPercentage);
        Assert.Equal(5, metrics.RemainingItems);
        Assert.False(metrics.EtaIsKnown);
    }

    [Fact]
    public void CalculateItemsPerMinute_ReturnsNull_ForSingleSample()
    {
        var throughput = EnrollmentProgressCalculator.CalculateItemsPerMinute(
        [
            new RecentEnrollmentCompletionSample
            {
                CreatedUtc = DateTime.UtcNow.AddMinutes(-5),
                CompletedUtc = DateTime.UtcNow,
            },
        ]);

        Assert.Null(throughput);
    }

    [Fact]
    public void CalculateItemsPerMinute_ComputesFromRecentCompletions()
    {
        var baseTime = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);
        var throughput = EnrollmentProgressCalculator.CalculateItemsPerMinute(
        [
            new RecentEnrollmentCompletionSample
            {
                CreatedUtc = baseTime.AddMinutes(-10),
                CompletedUtc = baseTime,
            },
            new RecentEnrollmentCompletionSample
            {
                CreatedUtc = baseTime.AddMinutes(-8),
                CompletedUtc = baseTime.AddMinutes(2),
            },
        ]);

        Assert.Equal(1, throughput);
    }
}
