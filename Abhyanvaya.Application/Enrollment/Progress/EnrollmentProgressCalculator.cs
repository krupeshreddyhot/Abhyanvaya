using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.ValueObjects;

namespace Abhyanvaya.Application.Enrollment.Progress;

public static class EnrollmentProgressCalculator
{
    public const int MinimumCompletionSamplesForEta = 2;
    public const int DefaultRecentSampleSize = 25;

    public static EnrollmentProgressMetrics BuildMetrics(
        StudentEnrollmentBatchCounters counters,
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions,
        int uploadingItems,
        DateTime utcNow)
    {
        var statistics = MapStatistics(counters);
        var remainingItems = Math.Max(0, statistics.Total - statistics.TerminalCount);
        var averageItemDuration = CalculateAverageItemDurationSeconds(recentCompletions);
        var averageStageDuration = CalculateAverageStageDurationSeconds(recentCompletions);
        var throughput = CalculateItemsPerMinute(recentCompletions);
        var eta = CalculateEta(utcNow, remainingItems, averageItemDuration, recentCompletions.Count);

        return new EnrollmentProgressMetrics
        {
            TotalItems = statistics.Total,
            PendingItems = statistics.Pending,
            DownloadingItems = statistics.Downloading,
            ValidatingItems = statistics.Validating,
            UploadingItems = uploadingItems,
            EmbeddingItems = statistics.Embedding,
            CompletedItems = statistics.Completed,
            RetryItems = statistics.RetryRequired,
            FailedItems = statistics.Failed,
            CancelledItems = statistics.Cancelled,
            CompletionPercentage = statistics.CompletionPercentage,
            AverageItemDurationSeconds = averageItemDuration,
            AverageStageDurationSeconds = averageStageDuration,
            ItemsPerMinute = throughput,
            RemainingItems = remainingItems,
            EstimatedCompletionUtc = eta.EstimatedCompletionUtc,
            EtaIsKnown = eta.IsKnown,
        };
    }

    public static EnrollmentStatistics MapStatistics(StudentEnrollmentBatchCounters counters) =>
        new(
            counters.TotalStudents,
            counters.PendingCount,
            counters.DownloadingCount,
            counters.ValidatingCount,
            counters.EmbeddingCount,
            counters.CompletedCount,
            counters.FailedCount,
            counters.RetryRequiredCount,
            counters.CancelledCount);

    public static EnrollmentEtaResult CalculateEta(
        DateTime utcNow,
        int remainingItems,
        double? averageItemDurationSeconds,
        int completedSampleCount)
    {
        if (remainingItems <= 0)
        {
            return EnrollmentEtaResult.Known(utcNow);
        }

        if (completedSampleCount < MinimumCompletionSamplesForEta
            || averageItemDurationSeconds is null or <= 0)
        {
            return EnrollmentEtaResult.Unknown();
        }

        var estimatedSeconds = remainingItems * averageItemDurationSeconds.Value;
        return EnrollmentEtaResult.Known(utcNow.AddSeconds(estimatedSeconds));
    }

    public static double? CalculateItemsPerMinute(
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions)
    {
        if (recentCompletions.Count < MinimumCompletionSamplesForEta)
        {
            return null;
        }

        var ordered = recentCompletions
            .OrderBy(sample => sample.CompletedUtc)
            .ToList();

        var earliest = ordered[0].CompletedUtc;
        var latest = ordered[^1].CompletedUtc;
        var elapsedMinutes = (latest - earliest).TotalMinutes;

        if (elapsedMinutes <= 0)
        {
            return null;
        }

        return Math.Round(ordered.Count / elapsedMinutes, 2);
    }

    public static double? CalculateAverageItemDurationSeconds(
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions)
    {
        if (recentCompletions.Count < MinimumCompletionSamplesForEta)
        {
            return null;
        }

        var durations = recentCompletions
            .Select(sample => (sample.CompletedUtc - sample.CreatedUtc).TotalSeconds)
            .Where(seconds => seconds >= 0)
            .ToList();

        if (durations.Count < MinimumCompletionSamplesForEta)
        {
            return null;
        }

        return Math.Round(durations.Average(), 2);
    }

    public static double? CalculateAverageStageDurationSeconds(
        IReadOnlyList<RecentEnrollmentCompletionSample> recentCompletions)
    {
        var durations = recentCompletions
            .Where(sample => sample.DownloadStartedUtc.HasValue && sample.DownloadedUtc.HasValue)
            .Select(sample => (sample.DownloadedUtc!.Value - sample.DownloadStartedUtc!.Value).TotalSeconds)
            .Where(seconds => seconds >= 0)
            .ToList();

        if (durations.Count == 0)
        {
            return null;
        }

        return Math.Round(durations.Average(), 2);
    }
}
