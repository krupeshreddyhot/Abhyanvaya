using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.AttendanceRecovery;

/// <summary>
/// Maps persisted <see cref="AttendanceSessionStatus"/> (+ image/review signals) to additive
/// <see cref="AttendanceWorkflowStatus"/> without replacing Status.
/// </summary>
public static class AttendanceWorkflowMapper
{
    public static AttendanceWorkflowStatus FromSession(
        AttendanceSession session,
        bool hasImages = false,
        bool hasFailedImages = false,
        bool reviewStarted = false)
    {
        if (session.WorkflowExpiredUtc.HasValue)
            return AttendanceWorkflowStatus.Expired;

        return session.Status switch
        {
            AttendanceSessionStatus.Cancelled => AttendanceWorkflowStatus.Cancelled,
            AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed
                => AttendanceWorkflowStatus.AttendanceFinalized,
            AttendanceSessionStatus.Failed when hasFailedImages && !hasImages
                => AttendanceWorkflowStatus.UploadFailed,
            AttendanceSessionStatus.Failed => AttendanceWorkflowStatus.RecognitionFailed,
            AttendanceSessionStatus.Processing => AttendanceWorkflowStatus.RecognitionRunning,
            AttendanceSessionStatus.AwaitingReview when reviewStarted
                => AttendanceWorkflowStatus.ReviewInProgress,
            AttendanceSessionStatus.AwaitingReview => AttendanceWorkflowStatus.ReviewPending,
            AttendanceSessionStatus.Pending when hasImages => AttendanceWorkflowStatus.ImagesUploaded,
            AttendanceSessionStatus.Draft when hasImages => AttendanceWorkflowStatus.ImagesUploaded,
            AttendanceSessionStatus.Draft or AttendanceSessionStatus.Pending
                => AttendanceWorkflowStatus.Created,
            _ => session.WorkflowStatus
        };
    }

    public static string CurrentStage(AttendanceWorkflowStatus status) => status switch
    {
        AttendanceWorkflowStatus.Created => "Created",
        AttendanceWorkflowStatus.ImagesUploaded => "ImagesUploaded",
        AttendanceWorkflowStatus.RecognitionRunning => "RecognitionRunning",
        AttendanceWorkflowStatus.RecognitionCompleted => "RecognitionCompleted",
        AttendanceWorkflowStatus.ReviewPending => "ReviewPending",
        AttendanceWorkflowStatus.ReviewInProgress => "ReviewInProgress",
        AttendanceWorkflowStatus.ReadyForFinalization => "ReadyForFinalization",
        AttendanceWorkflowStatus.AttendanceFinalized => "AttendanceFinalized",
        AttendanceWorkflowStatus.Cancelled => "Cancelled",
        AttendanceWorkflowStatus.RecognitionFailed => "RecognitionFailed",
        AttendanceWorkflowStatus.UploadFailed => "UploadFailed",
        AttendanceWorkflowStatus.Expired => "Expired",
        _ => status.ToString()
    };

    public static string ResumePath(Guid sessionId, AttendanceWorkflowStatus workflow) =>
        workflow is AttendanceWorkflowStatus.ReviewPending
            or AttendanceWorkflowStatus.ReviewInProgress
            or AttendanceWorkflowStatus.ReadyForFinalization
            or AttendanceWorkflowStatus.RecognitionCompleted
            ? $"/attendance/sessions/{sessionId}/review"
            : $"/attendance/sessions/{sessionId}/review";
}
