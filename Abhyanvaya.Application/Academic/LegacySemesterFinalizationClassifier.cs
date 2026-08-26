using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3D —
/// Fail-closed disposition classifier for remaining NULL-group Semesters. No mutation.
/// </summary>
public static class LegacySemesterFinalizationClassifier
{
    public sealed record Input(
        int SemesterId,
        int Number,
        int ActiveGroupCountOnCourse,
        bool HasDuplicateLegacyNumberOnCourse,
        int StudentReferenceCount,
        int TeachingGroupReferenceCount,
        int AttendanceReferenceCount,
        int SubjectAllocationReferenceCount,
        int TimetableEntryReferenceCount,
        int SubjectReferenceCount,
        int SectionReferenceCount,
        string? Prompt3ADecisionCode);

    public static string ToCode(LegacySemesterFinalizationDisposition d) => d switch
    {
        LegacySemesterFinalizationDisposition.AlreadyGroupSpecific => "ALREADY_GROUP_SPECIFIC",
        LegacySemesterFinalizationDisposition.SafeSingleGroupMapping => "SAFE_SINGLE_GROUP_MAPPING",
        LegacySemesterFinalizationDisposition.SplitRequired => "SPLIT_REQUIRED",
        LegacySemesterFinalizationDisposition.ManualMappingRequired => "MANUAL_MAPPING_REQUIRED",
        LegacySemesterFinalizationDisposition.DuplicateReview => "DUPLICATE_REVIEW",
        LegacySemesterFinalizationDisposition.HistoricalRetain => "HISTORICAL_RETAIN",
        LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference => "BLOCKED_BY_TEACHING_GROUP_REFERENCE",
        LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision => "UNKNOWN_REQUIRES_ARCHITECT_DECISION",
        _ => d.ToString().ToUpperInvariant(),
    };

    public static (LegacySemesterFinalizationDisposition Disposition, string Evidence) Classify(Input input)
    {
        if (input.TeachingGroupReferenceCount > 0)
        {
            return (
                LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference,
                $"TeachingGroup refs={input.TeachingGroupReferenceCount}; TG architecture frozen — identify-only until separate TG prompt.");
        }

        if (input.HasDuplicateLegacyNumberOnCourse
            || string.Equals(input.Prompt3ADecisionCode, "DUPLICATE_REVIEW", StringComparison.OrdinalIgnoreCase))
        {
            return (
                LegacySemesterFinalizationDisposition.DuplicateReview,
                "Duplicate legacy Semester Number on Course (or Prompt 3A DUPLICATE_REVIEW); no auto-merge.");
        }

        if (input.ActiveGroupCountOnCourse == 1
            && input.StudentReferenceCount == 0
            && DownstreamTotal(input) == 0)
        {
            return (
                LegacySemesterFinalizationDisposition.SafeSingleGroupMapping,
                "Exactly one active Group on Course and zero downstream refs; candidate for future MAP_TO_SINGLE_GROUP (not executed).");
        }

        if (input.StudentReferenceCount > 0 && input.ActiveGroupCountOnCourse > 1)
        {
            return (
                LegacySemesterFinalizationDisposition.SplitRequired,
                "Students still reference this legacy Semester across a multi-Group Course; split required.");
        }

        if (DownstreamTotal(input) == 0
            && input.StudentReferenceCount == 0
            && input.ActiveGroupCountOnCourse > 1)
        {
            // Empty multi-group legacy — historical retain pending Architect decision (matches 3A RETAIN).
            if (string.Equals(input.Prompt3ADecisionCode, "RETAIN_LEGACY_PENDING_DECISION", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(input.Prompt3ADecisionCode))
            {
                return (
                    LegacySemesterFinalizationDisposition.HistoricalRetain,
                    "Zero students/downstream refs on multi-Group Course; Prompt 3A retain/pending — HISTORICAL_RETAIN pending Architect disposition.");
            }
        }

        if (input.ActiveGroupCountOnCourse > 1)
        {
            return (
                LegacySemesterFinalizationDisposition.ManualMappingRequired,
                "Multi-Group Course with remaining legacy NULL GroupId; fail closed — manual mapping required.");
        }

        if (input.ActiveGroupCountOnCourse == 0)
        {
            return (
                LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision,
                "Course has zero active Groups; cannot map.");
        }

        return (
            LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision,
            "Insufficient deterministic evidence for automatic disposition.");
    }

    private static int DownstreamTotal(Input input)
        => input.AttendanceReferenceCount
           + input.SubjectAllocationReferenceCount
           + input.TimetableEntryReferenceCount
           + input.SubjectReferenceCount
           + input.SectionReferenceCount
           + input.TeachingGroupReferenceCount;
}
