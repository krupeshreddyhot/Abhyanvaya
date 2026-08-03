using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.AttendanceRecovery;

/// <summary>
/// AI22.8.5.2 — enterprise priority calculation for pending queue sorting.
/// Read-only; does not change workflow Status.
/// </summary>
public sealed class AttendanceSessionPrioritySnapshot
{
    public int PriorityScore { get; init; }
    public string PriorityBand { get; init; } = "RecentlyStarted";
    public double ExpectedRemainingMinutes { get; init; }
    public double AgeMinutes { get; init; }
    public int RetryCount { get; init; }
    public int FailureCount { get; init; }
}

public static class AttendanceSessionPriorityEngine
{
    // Higher score = higher priority (Failed first).
    private static readonly Dictionary<string, int> BandBase = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Failed"] = 9000,
        ["NeedsReview"] = 8000,
        ["RecognitionReady"] = 7000,
        ["RecognitionRunning"] = 6000,
        ["ExpiredSoon"] = 5000,
        ["RecentlyStarted"] = 4000
    };

    public static AttendanceSessionPrioritySnapshot Calculate(
        AttendanceSession session,
        AttendanceWorkflowStatus? workflow = null,
        int expirationHours = 48)
    {
        var w = workflow ?? AttendanceWorkflowMapper.FromSession(session, hasImages: true);
        var last = session.LastActivityUtc ?? session.StartedUtc ?? session.CreatedUtc;
        var ageMinutes = Math.Max(0, (DateTime.UtcNow - session.CreatedUtc).TotalMinutes);
        var idleMinutes = Math.Max(0, (DateTime.UtcNow - last).TotalMinutes);
        var failureCount = session.RetryCount + (string.IsNullOrWhiteSpace(session.ProcessingError) ? 0 : 1);
        var band = ResolveBand(w, idleMinutes, expirationHours);
        var baseScore = BandBase.GetValueOrDefault(band, 4000);

        // Age + retries boost within band; recent activity slightly lowers urgency for running jobs.
        var score = baseScore
            + (int)Math.Min(800, ageMinutes)
            + session.RetryCount * 40
            + failureCount * 25
            - (w == AttendanceWorkflowStatus.RecognitionRunning ? (int)Math.Min(100, idleMinutes) : 0);

        return new AttendanceSessionPrioritySnapshot
        {
            PriorityScore = Math.Max(0, score),
            PriorityBand = band,
            ExpectedRemainingMinutes = EstimateRemaining(w, idleMinutes),
            AgeMinutes = ageMinutes,
            RetryCount = session.RetryCount,
            FailureCount = failureCount
        };
    }

    public static IReadOnlyList<T> SortByPriority<T>(
        IEnumerable<T> items,
        Func<T, int> scoreSelector,
        Func<T, DateTime?>? activitySelector = null)
    {
        return items
            .OrderByDescending(scoreSelector)
            .ThenByDescending(x => activitySelector?.Invoke(x) ?? DateTime.MinValue)
            .ToList();
    }

    public static string ResolveBand(
        AttendanceWorkflowStatus workflow,
        double idleMinutes,
        int expirationHours)
    {
        if (workflow is AttendanceWorkflowStatus.RecognitionFailed or AttendanceWorkflowStatus.UploadFailed)
            return "Failed";

        if (workflow is AttendanceWorkflowStatus.ReviewPending
            or AttendanceWorkflowStatus.ReviewInProgress
            or AttendanceWorkflowStatus.ReadyForFinalization)
            return "NeedsReview";

        if (workflow is AttendanceWorkflowStatus.Created
            or AttendanceWorkflowStatus.ImagesUploaded
            or AttendanceWorkflowStatus.RecognitionCompleted)
            return "RecognitionReady";

        if (workflow == AttendanceWorkflowStatus.RecognitionRunning)
            return "RecognitionRunning";

        var expireSoonMinutes = Math.Max(60, expirationHours * 60 * 0.15);
        if (workflow == AttendanceWorkflowStatus.Expired || idleMinutes >= expireSoonMinutes)
            return "ExpiredSoon";

        return "RecentlyStarted";
    }

    private static double EstimateRemaining(AttendanceWorkflowStatus workflow, double idleMinutes) => workflow switch
    {
        AttendanceWorkflowStatus.RecognitionRunning => Math.Max(2, 12 - Math.Min(10, idleMinutes / 5)),
        AttendanceWorkflowStatus.ReviewPending or AttendanceWorkflowStatus.ReviewInProgress => 8,
        AttendanceWorkflowStatus.ReadyForFinalization or AttendanceWorkflowStatus.RecognitionCompleted => 3,
        AttendanceWorkflowStatus.RecognitionFailed or AttendanceWorkflowStatus.UploadFailed => 10,
        AttendanceWorkflowStatus.ImagesUploaded or AttendanceWorkflowStatus.Created => 15,
        _ => 5
    };
}
