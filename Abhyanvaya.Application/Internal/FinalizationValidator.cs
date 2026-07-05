using Abhyanvaya.Application.Exceptions;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// Shared finalization readiness and validation rules.
/// </summary>
internal static class FinalizationValidator
{
    internal static bool IsPendingRecognition(AttendanceRecognition recognition) =>
        !recognition.VerifiedByTeacher
        || recognition.RecognitionStatus is RecognitionStatus.Unknown or RecognitionStatus.LowConfidence;

    internal static List<string> BuildBlockingReasons(
        AttendanceSession session,
        IReadOnlyList<AttendanceRecognition> recognitions,
        bool attendanceAlreadyGenerated)
    {
        var blockers = new List<string>();

        if (session.Status == AttendanceSessionStatus.Cancelled)
        {
            blockers.Add("Cancelled sessions cannot be finalized.");
            return blockers;
        }

        if (session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
        {
            if (attendanceAlreadyGenerated)
            {
                blockers.Add("Official attendance has already been generated for this session.");
            }

            return blockers;
        }

        if (session.Status == AttendanceSessionStatus.Processing)
        {
            blockers.Add("Recognition pipeline is still running.");
        }

        if (session.Status != AttendanceSessionStatus.AwaitingReview)
        {
            blockers.Add("Session must be awaiting teacher review before finalization.");
        }

        if (attendanceAlreadyGenerated)
        {
            blockers.Add("Attendance has already been generated for this session.");
        }

        if (recognitions.Any(r => r.RecognitionStatus == RecognitionStatus.Unknown))
        {
            blockers.Add("Unknown recognitions must be reviewed before finalization.");
        }

        if (recognitions.Any(r => r.RecognitionStatus == RecognitionStatus.LowConfidence))
        {
            blockers.Add("Low-confidence recognitions must be reviewed before finalization.");
        }

        if (recognitions.Any(r => !r.VerifiedByTeacher))
        {
            blockers.Add("All recognitions must be reviewed before finalization.");
        }

        return blockers;
    }

    internal static void ValidateOrThrow(
        AttendanceSession session,
        IReadOnlyList<AttendanceRecognition> recognitions,
        bool attendanceAlreadyGenerated)
    {
        var blockers = BuildBlockingReasons(session, recognitions, attendanceAlreadyGenerated);
        if (blockers.Count == 0)
        {
            return;
        }

        throw new ValidationException(new Dictionary<string, string[]>
        {
            ["finalization"] = blockers.ToArray()
        });
    }
}
