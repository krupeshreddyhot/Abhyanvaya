using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2B —
/// Fail-closed classification of legacy Semesters. Never invents a Group assignment.
/// </summary>
public static class LegacySemesterMigrationClassifier
{
    public sealed record ActiveGroupInfo(int Id, string Code, string Name);

    public sealed record Input(
        int SemesterId,
        int CourseId,
        string CourseName,
        bool CourseExists,
        bool CourseDeleted,
        int Number,
        string Name,
        int? CurrentGroupId,
        string? CurrentGroupName,
        IReadOnlyList<ActiveGroupInfo> ActiveGroupsOnCourse,
        int StudentReferenceCount,
        IReadOnlyDictionary<int, int> StudentCountByGroupId,
        int AttendanceReferenceCount,
        int SubjectAllocationReferenceCount,
        int TimetableEntryReferenceCount,
        int SubjectReferenceCount,
        int SectionReferenceCount,
        int TeachingGroupReferenceCount,
        bool HasDuplicateLegacyNumberOnCourse);

    public static LegacySemesterMigrationRowDto Classify(Input input)
    {
        if (!input.CourseExists || input.CourseDeleted || input.CourseId <= 0)
        {
            return Build(
                input,
                LegacySemesterClassification.InvalidData,
                LegacySemesterMigrationAction.InvalidDataReview,
                "Course is missing, deleted, or invalid.");
        }

        if (input.CurrentGroupId is > 0)
        {
            return Build(
                input,
                LegacySemesterClassification.AlreadyGroupSpecific,
                LegacySemesterMigrationAction.AlreadyGroupSpecific,
                "Semester already has GroupId; no legacy null-group migration action.");
        }

        // Remaining: GroupId IS NULL (legacy course-wide)
        var groups = input.ActiveGroupsOnCourse;
        if (groups.Count == 0)
        {
            return Build(
                input,
                LegacySemesterClassification.OrphanNoGroup,
                LegacySemesterMigrationAction.OrphanReviewRequired,
                "Course has zero active Groups; cannot map Semester.");
        }

        if (input.HasDuplicateLegacyNumberOnCourse)
        {
            return Build(
                input,
                LegacySemesterClassification.AmbiguousMultiGroup,
                LegacySemesterMigrationAction.ManualMappingRequired,
                "Duplicate legacy Semester Number on the same Course; fail closed — no auto-map.");
        }

        if (groups.Count == 1)
        {
            return Build(
                input,
                LegacySemesterClassification.DeterministicSingleGroup,
                LegacySemesterMigrationAction.MapSingleGroup,
                $"Course has exactly one active Group ({groups[0].Name}); deterministic candidate only — not executed in Prompt 2B.");
        }

        // Multi-group: never auto-map. Student distribution is evidence only.
        var studentGroupDistinct = input.StudentCountByGroupId.Count(kv => kv.Value > 0);
        if (studentGroupDistinct > 1)
        {
            return Build(
                input,
                LegacySemesterClassification.AmbiguousMultiGroup,
                LegacySemesterMigrationAction.SplitRequired,
                "Course has multiple Groups and Students referencing this Semester span multiple Groups; split worksheet required.");
        }

        return Build(
            input,
            LegacySemesterClassification.AmbiguousMultiGroup,
            LegacySemesterMigrationAction.ManualMappingRequired,
            "Course has multiple Groups and Semester.GroupId is NULL; no authoritative Group — manual mapping required.");
    }

    private static LegacySemesterMigrationRowDto Build(
        Input input,
        LegacySemesterClassification classification,
        LegacySemesterMigrationAction action,
        string reason)
    {
        var candidates = input.ActiveGroupsOnCourse
            .Select(g => new LegacySemesterCandidateGroupDto
            {
                GroupId = g.Id,
                Code = g.Code,
                Name = g.Name,
                StudentReferenceCount = input.StudentCountByGroupId.TryGetValue(g.Id, out var n) ? n : 0,
            })
            .OrderBy(g => g.Name)
            .ToList();

        return new LegacySemesterMigrationRowDto
        {
            SemesterId = input.SemesterId,
            CourseId = input.CourseId,
            CourseName = input.CourseName,
            Number = input.Number,
            Name = input.Name,
            CurrentGroupId = input.CurrentGroupId,
            CurrentGroupName = input.CurrentGroupName,
            CandidateGroups = candidates,
            Classification = classification,
            ClassificationCode = ToCode(classification),
            StudentReferenceCount = input.StudentReferenceCount,
            AttendanceReferenceCount = input.AttendanceReferenceCount,
            SubjectAllocationReferenceCount = input.SubjectAllocationReferenceCount,
            TimetableEntryReferenceCount = input.TimetableEntryReferenceCount,
            SubjectReferenceCount = input.SubjectReferenceCount,
            SectionReferenceCount = input.SectionReferenceCount,
            TeachingGroupReferenceCount = input.TeachingGroupReferenceCount,
            HasDuplicateLegacyNumberOnCourse = input.HasDuplicateLegacyNumberOnCourse,
            MigrationAction = action,
            MigrationActionCode = ToActionCode(action),
            Reason = reason,
        };
    }

    public static string ToCode(LegacySemesterClassification c) => c switch
    {
        LegacySemesterClassification.DeterministicSingleGroup => "DETERMINISTIC_SINGLE_GROUP",
        LegacySemesterClassification.ExplicitExistingGroupReference => "EXPLICIT_EXISTING_GROUP_REFERENCE",
        LegacySemesterClassification.AmbiguousMultiGroup => "AMBIGUOUS_MULTI_GROUP",
        LegacySemesterClassification.OrphanNoGroup => "ORPHAN_NO_GROUP",
        LegacySemesterClassification.InvalidData => "INVALID_DATA",
        LegacySemesterClassification.AlreadyGroupSpecific => "ALREADY_GROUP_SPECIFIC",
        _ => c.ToString().ToUpperInvariant(),
    };

    public static string ToActionCode(LegacySemesterMigrationAction a) => a switch
    {
        LegacySemesterMigrationAction.MapSingleGroup => "MAP_SINGLE_GROUP",
        LegacySemesterMigrationAction.SplitRequired => "SPLIT_REQUIRED",
        LegacySemesterMigrationAction.ManualMappingRequired => "MANUAL_MAPPING_REQUIRED",
        LegacySemesterMigrationAction.OrphanReviewRequired => "ORPHAN_REVIEW_REQUIRED",
        LegacySemesterMigrationAction.InvalidDataReview => "INVALID_DATA_REVIEW",
        LegacySemesterMigrationAction.AlreadyGroupSpecific => "ALREADY_GROUP_SPECIFIC",
        LegacySemesterMigrationAction.NoAction => "NO_ACTION",
        _ => a.ToString().ToUpperInvariant(),
    };
}
