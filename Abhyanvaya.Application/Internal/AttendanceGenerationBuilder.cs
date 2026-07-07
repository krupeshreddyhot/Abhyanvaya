using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// Pure attendance generation from reviewed recognitions. No persistence.
/// </summary>
internal static class AttendanceGenerationBuilder
{
    internal sealed record BuildInput(
        Guid AttendanceSessionId,
        IReadOnlyList<AttendanceRecognition> Recognitions,
        HashSet<int> RosterStudentIds,
        HashSet<int> ExistingAttendanceStudentIds);

    internal sealed record BuildOutput(
        IReadOnlyList<AttendanceRecognition> PresentRecognitions,
        IReadOnlyList<int> AbsentStudentIds,
        AttendanceBuildSummaryDto Summary);

    internal static BuildOutput Build(BuildInput input)
    {
        var counts = AttendanceRecognitionMetrics.CountByStatus(input.Recognitions);
        var verifiedPresent = SelectVerifiedPresentRecognitions(input.Recognitions);
        var presentStudentIds = verifiedPresent
            .Select(r => r.StudentId!.Value)
            .Where(input.RosterStudentIds.Contains)
            .ToHashSet();

        var existingStudentIds = new HashSet<int>(input.ExistingAttendanceStudentIds);
        var presentRecognitions = new List<AttendanceRecognition>();
        var absentStudentIds = new List<int>();
        var presentCount = 0;
        var absentCount = 0;

        foreach (var recognition in verifiedPresent)
        {
            var studentId = recognition.StudentId!.Value;
            if (!input.RosterStudentIds.Contains(studentId) || existingStudentIds.Contains(studentId))
            {
                continue;
            }

            presentRecognitions.Add(recognition);
            existingStudentIds.Add(studentId);
            presentCount++;
        }

        foreach (var studentId in input.RosterStudentIds)
        {
            if (presentStudentIds.Contains(studentId) || existingStudentIds.Contains(studentId))
            {
                continue;
            }

            absentStudentIds.Add(studentId);
            existingStudentIds.Add(studentId);
            absentCount++;
        }

        var summary = new AttendanceBuildSummaryDto
        {
            AttendanceSessionId = input.AttendanceSessionId,
            Present = presentCount,
            Absent = absentCount,
            Ignored = counts.IgnoredCount,
            Rejected = counts.RejectedCount,
            Unknown = counts.UnknownCount,
            ManualCorrections = input.Recognitions.Count(r => r.TeacherOverride),
            TotalStudents = input.RosterStudentIds.Count
        };

        return new BuildOutput(presentRecognitions, absentStudentIds, summary);
    }

    internal static List<AttendanceRecognition> SelectVerifiedPresentRecognitions(
        IReadOnlyList<AttendanceRecognition> recognitions) =>
        recognitions
            .Where(r =>
                r.VerifiedByTeacher
                && r.StudentId.HasValue
                && (r.RecognitionStatus == RecognitionStatus.Recognized
                    || r.RecognitionStatus == RecognitionStatus.ManuallyAssigned))
            .GroupBy(r => r.StudentId!.Value)
            .Select(g => g.OrderBy(r => r.FaceNumber).First())
            .ToList();
}
