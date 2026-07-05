using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Internal;

/// <summary>Read-only projection from session + recognition counts to live status DTO fields.</summary>
internal static class AttendanceSessionStatusMapper
{
    internal static AttendanceSessionStatusDto Map(
        AttendanceSession session,
        int recognitionRowCount,
        int reviewedFaceCount,
        DateTime utcNow)
    {
        var detectedFaces = session.DetectedFaces > 0 ? session.DetectedFaces : recognitionRowCount;
        var matchedFaces = session.RecognizedCount + session.ManualAssignmentCount;
        if (matchedFaces <= 0 && recognitionRowCount > 0)
        {
            matchedFaces = recognitionRowCount;
        }

        decimal? recognitionAccuracy = detectedFaces <= 0
            ? null
            : decimal.Round((decimal)matchedFaces / detectedFaces * 100m, 2, MidpointRounding.AwayFromZero);

        var queueStatus = MapQueueStatus(session, recognitionRowCount);
        var workflowStep = MapWorkflowStep(session.Status, queueStatus);
        var startedUtc = session.StartedUtc ?? session.ImageMetadata.UploadedUtc;
        var elapsedMs = startedUtc.HasValue
            ? (long)Math.Max(0, (utcNow - startedUtc.Value).TotalMilliseconds)
            : (long?)null;

        var progressPercent = ComputeProgressPercent(session.Status, queueStatus, detectedFaces, matchedFaces);
        var stage = MapStageLabel(queueStatus);
        var operation = MapOperationLabel(queueStatus, detectedFaces, matchedFaces);
        var messages = BuildMessages(session, queueStatus, detectedFaces, matchedFaces);
        var (errorCode, _) = MapError(session);

        return new AttendanceSessionStatusDto
        {
            AttendanceSessionId = session.Id,
            Status = (int)session.Status,
            WorkflowStep = workflowStep,
            RecognitionQueueStatus = queueStatus,
            DetectedFaces = detectedFaces,
            MatchedFaces = matchedFaces,
            ReviewedFaces = reviewedFaceCount,
            RecognitionAccuracy = recognitionAccuracy,
            StartedUtc = startedUtc,
            LastUpdatedUtc = session.CompletedUtc ?? session.ImageMetadata.UploadedUtc ?? session.CreatedUtc,
            ElapsedMilliseconds = elapsedMs,
            RecognitionProgressPercent = progressPercent,
            CurrentStage = stage,
            CurrentOperation = operation,
            EstimatedRemainingMilliseconds = EstimateRemainingMilliseconds(session.Status, queueStatus, elapsedMs),
            CurrentFileName = session.OriginalFileName,
            Messages = messages,
            ErrorCode = errorCode,
            ProcessingError = session.ProcessingError,
        };
    }

    internal static RecognitionQueueStatus MapQueueStatus(AttendanceSession session, int recognitionRowCount)
    {
        return session.Status switch
        {
            AttendanceSessionStatus.Draft => RecognitionQueueStatus.Waiting,
            AttendanceSessionStatus.Pending => RecognitionQueueStatus.Queued,
            AttendanceSessionStatus.Processing when session.StartedUtc == null => RecognitionQueueStatus.WorkerPicked,
            AttendanceSessionStatus.Processing when session.DetectedFaces <= 0 && recognitionRowCount == 0 =>
                RecognitionQueueStatus.Detecting,
            AttendanceSessionStatus.Processing when session.RecognizedCount + session.ManualAssignmentCount <
                Math.Max(session.DetectedFaces, recognitionRowCount) => RecognitionQueueStatus.Matching,
            AttendanceSessionStatus.Processing => RecognitionQueueStatus.Saving,
            AttendanceSessionStatus.AwaitingReview => RecognitionQueueStatus.AwaitingReview,
            AttendanceSessionStatus.Failed => RecognitionQueueStatus.Failed,
            AttendanceSessionStatus.Cancelled => RecognitionQueueStatus.Cancelled,
            AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed => RecognitionQueueStatus.Completed,
            _ => RecognitionQueueStatus.Waiting,
        };
    }

    internal static AiWorkflowStep MapWorkflowStep(
        AttendanceSessionStatus status,
        RecognitionQueueStatus queueStatus)
    {
        return status switch
        {
            AttendanceSessionStatus.Draft => AiWorkflowStep.Upload,
            AttendanceSessionStatus.Pending => AiWorkflowStep.Upload,
            AttendanceSessionStatus.Processing when queueStatus is RecognitionQueueStatus.Matching
                or RecognitionQueueStatus.Saving => AiWorkflowStep.Match,
            AttendanceSessionStatus.Processing => AiWorkflowStep.Detect,
            AttendanceSessionStatus.AwaitingReview => AiWorkflowStep.Review,
            AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed => AiWorkflowStep.Finalize,
            AttendanceSessionStatus.Failed or AttendanceSessionStatus.Cancelled => AiWorkflowStep.Detect,
            _ => AiWorkflowStep.Upload,
        };
    }

    private static decimal ComputeProgressPercent(
        AttendanceSessionStatus status,
        RecognitionQueueStatus queueStatus,
        int detectedFaces,
        int matchedFaces)
    {
        if (status is AttendanceSessionStatus.AwaitingReview or AttendanceSessionStatus.Approved
            or AttendanceSessionStatus.Completed)
        {
            return 100m;
        }

        if (status == AttendanceSessionStatus.Pending)
        {
            return 15m;
        }

        if (status is AttendanceSessionStatus.Failed or AttendanceSessionStatus.Cancelled)
        {
            return 0m;
        }

        if (status != AttendanceSessionStatus.Processing)
        {
            return 0m;
        }

        return queueStatus switch
        {
            RecognitionQueueStatus.WorkerPicked => 25m,
            RecognitionQueueStatus.Detecting => 45m,
            RecognitionQueueStatus.Matching => detectedFaces <= 0
                ? 60m
                : 60m + Math.Min(25m, (decimal)matchedFaces / detectedFaces * 25m),
            RecognitionQueueStatus.Saving => 90m,
            _ => 10m,
        };
    }

    private static string MapStageLabel(RecognitionQueueStatus queueStatus) =>
        queueStatus switch
        {
            RecognitionQueueStatus.Waiting => "Waiting for upload",
            RecognitionQueueStatus.Queued => "Queued",
            RecognitionQueueStatus.WorkerPicked => "Worker started",
            RecognitionQueueStatus.Detecting => "Detecting faces",
            RecognitionQueueStatus.Matching => "Matching students",
            RecognitionQueueStatus.Saving => "Building recognitions",
            RecognitionQueueStatus.AwaitingReview => "Preparing review",
            RecognitionQueueStatus.Completed => "Completed",
            RecognitionQueueStatus.Failed => "Failed",
            RecognitionQueueStatus.Cancelled => "Cancelled",
            _ => "Processing",
        };

    private static string MapOperationLabel(
        RecognitionQueueStatus queueStatus,
        int detectedFaces,
        int matchedFaces) =>
        queueStatus switch
        {
            RecognitionQueueStatus.Waiting => "Waiting for classroom photo…",
            RecognitionQueueStatus.Queued => "Upload complete — waiting for worker",
            RecognitionQueueStatus.WorkerPicked => "Worker picked up the job",
            RecognitionQueueStatus.Detecting => "Detecting faces in classroom photo…",
            RecognitionQueueStatus.Matching => $"Matching students ({matchedFaces}/{Math.Max(detectedFaces, 1)})…",
            RecognitionQueueStatus.Saving => "Saving recognition rows…",
            RecognitionQueueStatus.AwaitingReview => "Preparing teacher review…",
            RecognitionQueueStatus.Completed => "Recognition complete",
            RecognitionQueueStatus.Failed => "Recognition failed",
            RecognitionQueueStatus.Cancelled => "Session cancelled",
            _ => "Processing…",
        };

    private static int? EstimateRemainingMilliseconds(
        AttendanceSessionStatus status,
        RecognitionQueueStatus queueStatus,
        long? elapsedMs)
    {
        if (status is not AttendanceSessionStatus.Processing and not AttendanceSessionStatus.Pending)
        {
            return null;
        }

        if (!elapsedMs.HasValue)
        {
            return null;
        }

        var progress = queueStatus switch
        {
            RecognitionQueueStatus.Queued => 0.15,
            RecognitionQueueStatus.WorkerPicked => 0.25,
            RecognitionQueueStatus.Detecting => 0.45,
            RecognitionQueueStatus.Matching => 0.70,
            RecognitionQueueStatus.Saving => 0.90,
            _ => 0.10,
        };

        if (progress <= 0.05)
        {
            return null;
        }

        var estimatedTotal = (int)(elapsedMs.Value / progress);
        return Math.Max(0, estimatedTotal - (int)elapsedMs.Value);
    }

    private static IReadOnlyList<string> BuildMessages(
        AttendanceSession session,
        RecognitionQueueStatus queueStatus,
        int detectedFaces,
        int matchedFaces)
    {
        var messages = new List<string> { MapOperationLabel(queueStatus, detectedFaces, matchedFaces) };

        if (queueStatus == RecognitionQueueStatus.Detecting)
        {
            messages.Add("Generating face embeddings…");
        }

        if (queueStatus == RecognitionQueueStatus.Matching)
        {
            messages.Add("Comparing embeddings with enrolled students…");
        }

        if (detectedFaces > 0)
        {
            messages.Add($"{detectedFaces} face(s) detected");
        }

        if (matchedFaces > 0)
        {
            messages.Add($"{matchedFaces} student match(es) found");
        }

        if (!string.IsNullOrWhiteSpace(session.OriginalFileName))
        {
            messages.Add($"File: {session.OriginalFileName}");
        }

        return messages;
    }

    private static (string? ErrorCode, string? Message) MapError(AttendanceSession session)
    {
        if (session.Status != AttendanceSessionStatus.Failed &&
            session.Status != AttendanceSessionStatus.Cancelled)
        {
            return (null, null);
        }

        var message = session.ProcessingError ?? string.Empty;
        var lower = message.ToLowerInvariant();

        if (session.Status == AttendanceSessionStatus.Cancelled)
        {
            return ("Cancelled", message);
        }

        if (lower.Contains("timeout"))
        {
            return ("Timeout", message);
        }

        if (lower.Contains("no face") || lower.Contains("no faces"))
        {
            return ("NoFacesFound", message);
        }

        if (lower.Contains("blur"))
        {
            return ("ImageTooBlurry", message);
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            return ("RecognitionError", message);
        }

        return ("Failed", "Recognition failed.");
    }
}
