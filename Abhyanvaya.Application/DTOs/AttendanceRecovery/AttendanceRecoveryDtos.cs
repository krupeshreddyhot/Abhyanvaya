using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.AttendanceRecovery;

public sealed class PendingAttendanceBucketDto
{
    public IReadOnlyList<PendingAttendanceSessionDto> MyPendingSessions { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> TodaysPending { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> ReviewPending { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> RecognitionRunning { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> FailedSessions { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> ReadyToFinalize { get; init; } = [];
    public int TotalPending { get; init; }
}

public sealed class PendingAttendanceSessionDto
{
    public Guid SessionId { get; init; }
    public string ResumeToken { get; init; } = "";
    public AttendanceSessionStatus Status { get; init; }
    public AttendanceWorkflowStatus WorkflowStatus { get; init; }
    public string WorkflowStatusName => WorkflowStatus.ToString();
    public DateTime AttendanceDate { get; init; }
    public int CourseId { get; init; }
    public string? CourseName { get; init; }
    public int GroupId { get; init; }
    public string? GroupName { get; init; }
    public int SemesterId { get; init; }
    public string? SemesterName { get; init; }
    public int SubjectId { get; init; }
    public string? SubjectName { get; init; }
    public int? PeriodNumber { get; init; }
    public int? StaffId { get; init; }
    public string? StaffName { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? LastActivityUtc { get; init; }
    public double ElapsedMinutes { get; init; }
    public int RetryCount { get; init; }
    public string? FailureReason { get; init; }
    public string ResumePath { get; init; } = "";
    public bool IsExpired { get; init; }
    public string CurrentStage { get; init; } = "";

    // AI22.8.5 — enterprise queue card fields
    public string DisplayTitle { get; init; } = "";
    public string ScheduledTimeLabel { get; init; } = "";
    public string FriendlyWorkflowLabel =>
        AttendanceSessionDisplayLabels.Friendly(WorkflowStatus);
    public int PriorityScore { get; init; }
    public string PriorityBand { get; init; } = "RecentlyStarted";
    public double AgeMinutes { get; init; }
    public int FailureCount { get; init; }
    public double ExpectedRemainingMinutes { get; init; }
    public bool CanResume { get; init; }
    public bool CanRetry { get; init; }
    public bool CanFinalize { get; init; }
    public bool CanCancel { get; init; }
}

/// <summary>AI22.8.5 — shared friendly labels for workflow status (DTO-safe).</summary>
public static class AttendanceSessionDisplayLabels
{
    public static string Friendly(AttendanceWorkflowStatus workflow) => workflow switch
    {
        AttendanceWorkflowStatus.Created or AttendanceWorkflowStatus.ImagesUploaded
            => "Recognition Ready",
        AttendanceWorkflowStatus.RecognitionRunning => "Recognition Running",
        AttendanceWorkflowStatus.RecognitionCompleted => "Recognition Ready",
        AttendanceWorkflowStatus.ReviewPending or AttendanceWorkflowStatus.ReviewInProgress
            => "Review Pending",
        AttendanceWorkflowStatus.ReadyForFinalization => "Ready to Finalize",
        AttendanceWorkflowStatus.UploadFailed => "Failed Upload",
        AttendanceWorkflowStatus.RecognitionFailed => "Recognition Failed",
        AttendanceWorkflowStatus.AttendanceFinalized => "Completed",
        AttendanceWorkflowStatus.Cancelled => "Cancelled",
        AttendanceWorkflowStatus.Expired => "Expired",
        _ => workflow.ToString()
    };
}

public sealed class AttendanceResumeCheckpointDto
{
    public Guid SessionId { get; init; }
    public Guid? CurrentImageId { get; init; }
    public double? Zoom { get; init; }
    public string? FiltersJson { get; init; }
    public int? CurrentStudentId { get; init; }
    public int? ReviewPosition { get; init; }
    public Guid? CurrentBatchId { get; init; }
    public string ResumePath { get; init; } = "";
    public AttendanceWorkflowStatus WorkflowStatus { get; init; }
    public bool AutoStartRecognition => false;
}

public sealed class SaveResumeCheckpointRequest
{
    public Guid? CurrentImageId { get; init; }
    public double? Zoom { get; init; }
    public string? FiltersJson { get; init; }
    public int? CurrentStudentId { get; init; }
    public int? ReviewPosition { get; init; }
    public Guid? CurrentBatchId { get; init; }
}

public enum AttendanceRetryKind
{
    RetryRecognition = 1,
    RetryFailedImages = 2,
    RetryUpload = 3,
    RetryFinalization = 4,
    RetryEntireSession = 5
}

public sealed class AttendanceRetryRequest
{
    public AttendanceRetryKind Kind { get; init; }
    public Guid? ImageId { get; init; }
}

public sealed class AttendanceRetryResultDto
{
    public Guid SessionId { get; init; }
    public AttendanceRetryKind Kind { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
    public AttendanceWorkflowStatus WorkflowStatus { get; init; }
    public int RetryCount { get; init; }
    public bool RestartedCompletedStages => false;
}

public sealed class AttendanceRetryHistoryDto
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public string Stage { get; init; } = "";
    public string Action { get; init; } = "";
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime PerformedUtc { get; init; }
    public int PerformedBy { get; init; }
}

public sealed class AttendanceRecoverySearchRequest
{
    public Guid? SessionId { get; init; }
    public int? StaffId { get; init; }
    public int? StudentId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? SemesterId { get; init; }
    public int? SubjectId { get; init; }
    public DateTime? AttendanceDate { get; init; }
    public AttendanceSessionStatus? Status { get; init; }
    public AttendanceWorkflowStatus? WorkflowStatus { get; init; }
    public string? Query { get; init; }
    public int Take { get; init; } = 50;
}

public sealed class AttendanceRecoveryDashboardDto
{
    public int TodayCount { get; init; }
    public int YesterdayCount { get; init; }
    public int ProcessingCount { get; init; }
    public int FailedCount { get; init; }
    public int ReviewPendingCount { get; init; }
    public int FinalizationPendingCount { get; init; }
    public int ExpiredCount { get; init; }
    public IReadOnlyList<PendingAttendanceSessionDto> Sessions { get; init; } = [];
    public IReadOnlyList<RecoveryChartPointDto> ByStatus { get; init; } = [];
}

public sealed class RecoveryChartPointDto
{
    public string Label { get; init; } = "";
    public decimal Value { get; init; }
}

public sealed class AttendanceRecoveryAnalyticsDto
{
    public int PendingSessions { get; init; }
    public double? AverageReviewMinutes { get; init; }
    public double? AverageFinalizationMinutes { get; init; }
    public double AverageRetryCount { get; init; }
    public double FailureRatePercent { get; init; }
    public double RecognitionSuccessPercent { get; init; }
    public double ReviewCompletionPercent { get; init; }
    public IReadOnlyList<RecoveryChartPointDto> PendingTrend { get; init; } = [];
    public IReadOnlyList<RecoveryChartPointDto> FacultyProductivity { get; init; } = [];
    public bool ReusesExistingSessions => true;
}

public sealed class AutoResumePromptDto
{
    public bool ShouldPrompt { get; init; }
    public PendingAttendanceSessionDto? Session { get; init; }
    public string Message { get; init; } = "";
}

public sealed class AutoResumeDecisionRequest
{
    /// <summary>resume | continueReview | dismiss</summary>
    public string Decision { get; init; } = "dismiss";
    public Guid? SessionId { get; init; }
    public bool Remember { get; init; } = true;
}

public sealed class ExpirationOptionsDto
{
    public int DefaultExpirationHours { get; init; } = 48;
    public int[] AllowedHours { get; init; } = [24, 48, 72];
}

public sealed class AdminSessionActionRequest
{
    /// <summary>restore | archive | delete</summary>
    public string Action { get; init; } = "";
    public string? Reason { get; init; }
}

// --- AI22.8.5 queue / preferences / recovery center / ops ---

public sealed class PendingSessionQueueRequest
{
    public string? Query { get; init; }
    public string? WorkflowStatus { get; init; }
    public string? PriorityBand { get; init; }
    public string? SortBy { get; init; } = "priority";
    public bool? OnlyFailed { get; init; }
    public bool? OnlyNeedsReview { get; init; }
}

public sealed class PendingSessionQueueDto
{
    public IReadOnlyList<PendingAttendanceSessionDto> Items { get; init; } = [];
    public int Total { get; init; }
    public int FailedCount { get; init; }
    public int NeedsReviewCount { get; init; }
    public int RecognitionReadyCount { get; init; }
    public int RecognitionRunningCount { get; init; }
    public bool SortedByPriority { get; init; } = true;
}

public sealed class AttendanceRecoveryPreferenceDto
{
    public int StaffId { get; init; }
    public int AutoSaveFrequencySeconds { get; init; } = 30;
    public bool ResumeConfirmation { get; init; } = true;
    public string DefaultLandingPage { get; init; } = "pending";
    public bool NotificationsEnabled { get; init; } = true;
    public bool SessionTimeoutWarning { get; init; } = true;
    public int SessionTimeoutWarningMinutes { get; init; } = 30;
    public bool PromptOnLogin { get; init; } = true;
}

public sealed class UpsertAttendanceRecoveryPreferenceRequest
{
    public int? AutoSaveFrequencySeconds { get; init; }
    public bool? ResumeConfirmation { get; init; }
    public string? DefaultLandingPage { get; init; }
    public bool? NotificationsEnabled { get; init; }
    public bool? SessionTimeoutWarning { get; init; }
    public int? SessionTimeoutWarningMinutes { get; init; }
    public bool? PromptOnLogin { get; init; }
}

public sealed class FacultyRecoveryCenterDto
{
    public IReadOnlyList<PendingAttendanceSessionDto> TodaysSessions { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> Yesterday { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> NeedsAttention { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> Completed { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> Archived { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> SearchResults { get; init; } = [];
}

public sealed class AttendanceOperationsDashboardDto
{
    public IReadOnlyList<RecoveryChartPointDto> SessionsByStatus { get; init; } = [];
    public IReadOnlyList<PendingAttendanceSessionDto> LongestRunningSessions { get; init; } = [];
    public IReadOnlyList<RecoveryChartPointDto> FacultyProductivity { get; init; } = [];
    public double? AverageReviewTimeMinutes { get; init; }
    public double RecognitionFailureRatePercent { get; init; }
    public double RetrySuccessRatePercent { get; init; }
    public double FinalizationSlaPercent { get; init; }
    public IReadOnlyList<RecoveryChartPointDto> DepartmentDistribution { get; init; } = [];
    public IReadOnlyList<RecoveryChartPointDto> RoomDistribution { get; init; } = [];
    public IReadOnlyList<RecoveryChartPointDto> TopBusyFaculty { get; init; } = [];
}

public sealed class AttendanceOperationalAnalyticsDto
{
    public double? AverageRecognitionMinutes { get; init; }
    public double? AverageReviewMinutes { get; init; }
    public double? AverageFinalizationMinutes { get; init; }
    public int SessionsStarted { get; init; }
    public int SessionsCompleted { get; init; }
    public double RetryPercent { get; init; }
    public double FailurePercent { get; init; }
    public double ResumePercent { get; init; }
    public string? PeakUsageLabel { get; init; }
    public IReadOnlyList<RecoveryChartPointDto> DailyTrends { get; init; } = [];
    public IReadOnlyList<RecoveryChartPointDto> DepartmentTrends { get; init; } = [];
    public IReadOnlyList<RecoveryChartPointDto> FacultyTrends { get; init; } = [];
    public bool ReadOnly => true;
}

public sealed class AttendanceHealthAlertDto
{
    public string Code { get; init; } = "";
    public string Severity { get; init; } = "warning";
    public string Message { get; init; } = "";
    public Guid? SessionId { get; init; }
    public int? StaffId { get; init; }
    public DateTime DetectedUtc { get; init; } = DateTime.UtcNow;
}

public sealed class AttendanceHealthSnapshotDto
{
    public IReadOnlyList<AttendanceHealthAlertDto> Alerts { get; init; } = [];
    public int RecognitionStalled { get; init; }
    public int ReviewStalled { get; init; }
    public int Abandoned { get; init; }
    public int RepeatedFailures { get; init; }
    public int LargePendingQueues { get; init; }
    public int LongRunning { get; init; }
    public bool NeverAutoCancels => true;
}

public sealed class FacultyWorkspaceRecoverySummaryDto
{
    public int TodaysClasses { get; init; }
    public int PendingAttendance { get; init; }
    public int NeedsReview { get; init; }
    public int RecognitionRunning { get; init; }
    public int Completed { get; init; }
    public IReadOnlyList<PendingAttendanceSessionDto> TopPending { get; init; } = [];
}
