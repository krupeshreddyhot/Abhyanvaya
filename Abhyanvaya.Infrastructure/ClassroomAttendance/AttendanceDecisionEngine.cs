using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class AttendanceDecisionEngine : IAttendanceDecisionEngine
{
    private readonly IManualReviewService _manualReviewService;

    public AttendanceDecisionEngine(IManualReviewService manualReviewService)
    {
        _manualReviewService = manualReviewService;
    }

    public IReadOnlyList<AttendanceDecision> Decide(AttendanceSessionContext context)
    {
        var policy = context.Policy ?? throw new InvalidOperationException("Attendance policy is required.");
        var outcomes = context.Conflicts != null && context.RecognitionOutcomes != null
            ? context.RecognitionOutcomes
            : context.RecognitionOutcomes ?? Array.Empty<FaceRecognitionOutcome>();

        var decisions = new List<AttendanceDecision>();
        var assignedStudents = new HashSet<int>();

        foreach (var outcome in outcomes)
        {
            var recognition = outcome.RecognitionResult;
            var recognitionDecision = recognition?.Decision;

            if (recognitionDecision == null)
            {
                decisions.Add(CreateDecision(outcome, AttendanceDecisionType.Unknown, RecognitionStatus.Unknown, null, null, "No recognition decision."));
                continue;
            }

            var review = _manualReviewService.Evaluate(new ManualReviewRequest
            {
                SessionId = context.Session.SessionId,
                FaceIndex = outcome.FaceIndex,
                Reason = recognitionDecision.Reason ?? recognitionDecision.Status.ToString(),
                RecognitionId = recognition?.PersistedRecognitionId,
            });

            if (review.RequiresReview && policy.ManualReviewEnabled)
            {
                decisions.Add(CreateDecision(
                    outcome,
                    AttendanceDecisionType.ManualReview,
                    RecognitionStatus.LowConfidence,
                    recognitionDecision.StudentId,
                    recognitionDecision.Confidence,
                    review.ReviewReason,
                    requiresManualReview: true));
                continue;
            }

            var decisionType = MapDecision(recognitionDecision, policy, assignedStudents, context.Session.AttendanceDateUtc);

            if (decisionType == AttendanceDecisionType.Present && recognitionDecision.StudentId.HasValue)
            {
                assignedStudents.Add(recognitionDecision.StudentId.Value);
            }

            decisions.Add(CreateDecision(
                outcome,
                decisionType,
                MapRecognitionStatus(decisionType, recognitionDecision.Status),
                recognitionDecision.StudentId,
                recognitionDecision.Confidence,
                recognitionDecision.Reason));
        }

        return decisions;
    }

    private static AttendanceDecisionType MapDecision(
        RecognitionDecision recognitionDecision,
        IAttendancePolicy policy,
        IReadOnlySet<int> assignedStudents,
        DateTime attendanceDateUtc)
    {
        if (recognitionDecision.DecisionType == RecognitionDecisionType.Unknown
            || recognitionDecision.Status == RecognitionStatus.Unknown)
        {
            return AttendanceDecisionType.Unknown;
        }

        if (recognitionDecision.DecisionType == RecognitionDecisionType.Duplicate
            || (recognitionDecision.StudentId.HasValue && assignedStudents.Contains(recognitionDecision.StudentId.Value) && !policy.AllowDuplicateStudents))
        {
            return AttendanceDecisionType.Duplicate;
        }

        if (recognitionDecision.RequiresManualReview || recognitionDecision.DecisionType == RecognitionDecisionType.ManualReview)
        {
            return AttendanceDecisionType.ManualReview;
        }

        if (recognitionDecision.DecisionType == RecognitionDecisionType.LowConfidence)
        {
            return policy.ManualReviewEnabled
                ? AttendanceDecisionType.ManualReview
                : AttendanceDecisionType.Unknown;
        }

        if ((float)recognitionDecision.Confidence < policy.MinimumConfidence)
        {
            return AttendanceDecisionType.ManualReview;
        }

        if (IsLate(attendanceDateUtc, policy))
        {
            return AttendanceDecisionType.Late;
        }

        if (recognitionDecision.DecisionType == RecognitionDecisionType.Recognized)
        {
            return AttendanceDecisionType.Present;
        }

        return AttendanceDecisionType.Unknown;
    }

    private static bool IsLate(DateTime attendanceDateUtc, IAttendancePolicy policy)
    {
        if (policy.AttendanceWindowEnd == null)
        {
            return false;
        }

        var sessionStart = attendanceDateUtc.TimeOfDay;
        return sessionStart > policy.AttendanceWindowEnd.Value + policy.LateArrivalThreshold;
    }

    private static RecognitionStatus MapRecognitionStatus(AttendanceDecisionType type, RecognitionStatus original) =>
        type switch
        {
            AttendanceDecisionType.Present or AttendanceDecisionType.Late => RecognitionStatus.Recognized,
            AttendanceDecisionType.Duplicate => RecognitionStatus.Duplicate,
            AttendanceDecisionType.ManualReview => RecognitionStatus.LowConfidence,
            AttendanceDecisionType.Rejected => RecognitionStatus.Rejected,
            AttendanceDecisionType.Unknown => RecognitionStatus.Unknown,
            _ => original,
        };

    private static AttendanceDecision CreateDecision(
        FaceRecognitionOutcome outcome,
        AttendanceDecisionType decisionType,
        RecognitionStatus status,
        int? studentId,
        decimal? confidence,
        string? reason,
        bool requiresManualReview = false) =>
        new()
        {
            FaceIndex = outcome.FaceIndex,
            DecisionType = decisionType,
            StudentId = studentId,
            RecognitionId = outcome.RecognitionResult?.PersistedRecognitionId,
            Confidence = confidence,
            RecognitionStatus = status,
            Reason = reason,
            RequiresManualReview = requiresManualReview,
        };
}
