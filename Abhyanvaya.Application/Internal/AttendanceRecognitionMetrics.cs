using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// Shared recognition status counting for summary sync, analytics, and build reporting.
/// </summary>
internal static class AttendanceRecognitionMetrics
{
    internal readonly record struct StatusCounts(
        int RecognizedCount,
        int UnknownCount,
        int RejectedCount,
        int IgnoredCount,
        int DuplicateCount,
        int ManualAssignmentCount,
        int LowConfidenceCount);

    internal static StatusCounts CountByStatus(IReadOnlyList<AttendanceRecognition> recognitions) =>
        new(
            recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Recognized),
            recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Unknown),
            recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Rejected),
            recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Ignored),
            recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Duplicate),
            recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.ManuallyAssigned),
            recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.LowConfidence));

    internal static decimal? ComputeAverageConfidence(IReadOnlyList<AttendanceRecognition> recognitions)
    {
        var confidenceScores = recognitions
            .Where(r => r.ConfidenceScore.HasValue)
            .Select(r => r.ConfidenceScore!.Value)
            .ToList();

        return confidenceScores.Count == 0
            ? null
            : decimal.Round(confidenceScores.Average(), 2, MidpointRounding.AwayFromZero);
    }

    internal static decimal? ComputeRecognitionCompletionPercent(IReadOnlyList<AttendanceRecognition> recognitions) =>
        recognitions.Count == 0
            ? null
            : decimal.Round(
                (decimal)recognitions.Count(r => r.VerifiedByTeacher) / recognitions.Count * 100m,
                2,
                MidpointRounding.AwayFromZero);

    internal static int CountTeacherCorrections(IReadOnlyList<AttendanceRecognition> recognitions) =>
        recognitions.Count(r => r.TeacherOverride);
}
