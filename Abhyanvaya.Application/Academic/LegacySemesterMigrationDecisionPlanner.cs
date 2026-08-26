using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3A —
/// Fail-closed migration decision planner. Never invents Group ownership; never mutates.
/// </summary>
public static class LegacySemesterMigrationDecisionPlanner
{
    public sealed record GroupInfo(int Id, string Name);

    public sealed record DownstreamCounts(
        IReadOnlyDictionary<int, int> StudentsByGroup,
        IReadOnlyDictionary<int, int> AttendanceByGroup,
        IReadOnlyDictionary<int, int> SubjectsByGroup,
        IReadOnlyDictionary<int, int> SectionsByGroup,
        IReadOnlyDictionary<int, int> SubjectAllocationsByGroup,
        IReadOnlyDictionary<int, int> TimetableEntriesByGroup,
        IReadOnlyDictionary<int, int> TeachingGroupsByGroup);

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
        IReadOnlyList<GroupInfo> ActiveGroupsOnCourse,
        DownstreamCounts Downstream,
        bool HasDuplicateLegacyNumberOnCourse);

    public static string ToDecisionCode(LegacySemesterMigrationDecision d) => d switch
    {
        LegacySemesterMigrationDecision.Split => "SPLIT",
        LegacySemesterMigrationDecision.MapToSingleGroup => "MAP_TO_SINGLE_GROUP",
        LegacySemesterMigrationDecision.RetainLegacyPendingDecision => "RETAIN_LEGACY_PENDING_DECISION",
        LegacySemesterMigrationDecision.DuplicateReview => "DUPLICATE_REVIEW",
        LegacySemesterMigrationDecision.AlreadyGroupSpecific => "ALREADY_GROUP_SPECIFIC",
        LegacySemesterMigrationDecision.InvalidData => "INVALID_DATA",
        _ => d.ToString().ToUpperInvariant(),
    };

    public static string ToDeterminismCode(DownstreamReferenceDeterminism d) => d switch
    {
        DownstreamReferenceDeterminism.DeterministicByEntityGroupId => "DETERMINISTIC_BY_ENTITY_GROUP_ID",
        DownstreamReferenceDeterminism.DeterministicByStudentGroupId => "DETERMINISTIC_BY_STUDENT_GROUP_ID",
        DownstreamReferenceDeterminism.ManualReviewRequired => "MANUAL_REVIEW_REQUIRED",
        DownstreamReferenceDeterminism.IdentifyOnlyDoNotMutate => "IDENTIFY_ONLY_DO_NOT_MUTATE",
        DownstreamReferenceDeterminism.NoReferences => "NO_REFERENCES",
        _ => d.ToString().ToUpperInvariant(),
    };

    public static LegacySemesterMigrationDecisionRowDto Plan(Input input)
    {
        if (!input.CourseExists || input.CourseDeleted || input.CourseId <= 0)
        {
            return Row(
                input,
                LegacySemesterMigrationDecision.InvalidData,
                "Course is missing, deleted, or invalid.",
                targetGroups: [],
                requiresApproval: true,
                mustNotModify: true);
        }

        if (input.CurrentGroupId is > 0)
        {
            return Row(
                input,
                LegacySemesterMigrationDecision.AlreadyGroupSpecific,
                "Semester already Group-specific; MUST NOT be modified.",
                targetGroups: [input.CurrentGroupId.Value],
                requiresApproval: false,
                mustNotModify: true);
        }

        if (input.HasDuplicateLegacyNumberOnCourse)
        {
            return Row(
                input,
                LegacySemesterMigrationDecision.DuplicateReview,
                "Duplicate legacy Number on Course; do not merge/delete/rename automatically.",
                targetGroups: input.ActiveGroupsOnCourse.Select(g => g.Id).ToList(),
                requiresApproval: true,
                mustNotModify: true);
        }

        var groups = input.ActiveGroupsOnCourse;
        if (groups.Count == 0)
        {
            return Row(
                input,
                LegacySemesterMigrationDecision.InvalidData,
                "Course has zero active Groups.",
                targetGroups: [],
                requiresApproval: true,
                mustNotModify: true);
        }

        if (groups.Count == 1)
        {
            return Row(
                input,
                LegacySemesterMigrationDecision.MapToSingleGroup,
                $"Course has exactly one Group ({groups[0].Name}); deterministic map candidate — not executed in Prompt 3A.",
                targetGroups: [groups[0].Id],
                requiresApproval: true,
                mustNotModify: false);
        }

        var studentDistinct = input.Downstream.StudentsByGroup.Count(kv => kv.Value > 0);
        if (studentDistinct > 1)
        {
            return Row(
                input,
                LegacySemesterMigrationDecision.Split,
                "Students referencing this Semester span multiple Groups. Student.GroupId is the authoritative future assignment criterion. Split plan only — no mutation in Prompt 3A.",
                targetGroups: groups.Select(g => g.Id).ToList(),
                requiresApproval: true,
                mustNotModify: false);
        }

        return Row(
            input,
            LegacySemesterMigrationDecision.RetainLegacyPendingDecision,
            "Multi-Group Course with NULL GroupId and no multi-Group student span; retain pending explicit administrative split/map decision.",
            targetGroups: groups.Select(g => g.Id).ToList(),
            requiresApproval: true,
            mustNotModify: true);
    }

    public static IReadOnlyList<DownstreamReferenceClassificationDto> ClassifyDownstream(Input input)
    {
        return
        [
            Classify("Student", input.Downstream.StudentsByGroup,
                DownstreamReferenceDeterminism.DeterministicByStudentGroupId,
                "Future Student.SemesterId assignment must follow Student.GroupId → target Group-specific Semester. Not applied in Prompt 3A."),
            Classify("AttendanceSession", input.Downstream.AttendanceByGroup,
                HasAuthoritativeGroup(input.Downstream.AttendanceByGroup)
                    ? DownstreamReferenceDeterminism.DeterministicByEntityGroupId
                    : DownstreamReferenceDeterminism.ManualReviewRequired,
                HasAuthoritativeGroup(input.Downstream.AttendanceByGroup)
                    ? "AttendanceSession.GroupId present; later remap may follow session GroupId. No Attendance mutation in Prompt 3A."
                    : "Attendance references lack authoritative Group association; MANUAL_REVIEW_REQUIRED."),
            Classify("Subject", input.Downstream.SubjectsByGroup,
                HasAuthoritativeGroup(input.Downstream.SubjectsByGroup)
                    ? DownstreamReferenceDeterminism.DeterministicByEntityGroupId
                    : DownstreamReferenceDeterminism.NoReferences,
                "Subject already stores GroupId; catalog subjects may split with Semester. Not mutated in Prompt 3A."),
            Classify("Section", input.Downstream.SectionsByGroup,
                HasAuthoritativeGroup(input.Downstream.SectionsByGroup)
                    ? DownstreamReferenceDeterminism.DeterministicByEntityGroupId
                    : DownstreamReferenceDeterminism.NoReferences,
                "Section.GroupId available when referenced. Identify only for later approved remaps."),
            Classify("SubjectAllocation", input.Downstream.SubjectAllocationsByGroup,
                HasAuthoritativeGroup(input.Downstream.SubjectAllocationsByGroup)
                    ? DownstreamReferenceDeterminism.DeterministicByEntityGroupId
                    : DownstreamReferenceDeterminism.NoReferences,
                "SA stores GroupId (scheduling denorm). Identify for later approved remaps; do not change TG architecture."),
            Classify("TimetableEntry", input.Downstream.TimetableEntriesByGroup,
                HasAuthoritativeGroup(input.Downstream.TimetableEntriesByGroup)
                    ? DownstreamReferenceDeterminism.DeterministicByEntityGroupId
                    : DownstreamReferenceDeterminism.NoReferences,
                "TimetableEntry stores GroupId; TeachingGroupId unchanged. No SectionId."),
            Classify("TeachingGroup", input.Downstream.TeachingGroupsByGroup,
                DownstreamReferenceDeterminism.IdentifyOnlyDoNotMutate,
                "Teaching Groups are FROZEN — identify affected TGs only; no create/infer/membership/TimetableSection changes."),
        ];
    }

    private static bool HasAuthoritativeGroup(IReadOnlyDictionary<int, int> byGroup)
        => byGroup.Any(kv => kv.Key > 0 && kv.Value > 0);

    private static DownstreamReferenceClassificationDto Classify(
        string entityType,
        IReadOnlyDictionary<int, int> byGroup,
        DownstreamReferenceDeterminism determinism,
        string notes)
    {
        var total = byGroup.Values.Sum();
        if (total == 0 && determinism != DownstreamReferenceDeterminism.IdentifyOnlyDoNotMutate)
        {
            determinism = DownstreamReferenceDeterminism.NoReferences;
            notes = "No references.";
        }

        return new DownstreamReferenceClassificationDto
        {
            EntityType = entityType,
            ReferenceCount = total,
            CountsByGroupId = byGroup,
            Determinism = determinism,
            DeterminismCode = ToDeterminismCode(determinism),
            Notes = notes,
        };
    }

    private static LegacySemesterMigrationDecisionRowDto Row(
        Input input,
        LegacySemesterMigrationDecision decision,
        string reason,
        IReadOnlyList<int> targetGroups,
        bool requiresApproval,
        bool mustNotModify)
    {
        var names = input.ActiveGroupsOnCourse
            .Where(g => targetGroups.Contains(g.Id) || decision == LegacySemesterMigrationDecision.AlreadyGroupSpecific)
            .Select(g => g.Name)
            .ToList();

        if (decision == LegacySemesterMigrationDecision.AlreadyGroupSpecific && input.CurrentGroupName is not null)
            names = [input.CurrentGroupName];

        return new LegacySemesterMigrationDecisionRowDto
        {
            SemesterId = input.SemesterId,
            CourseId = input.CourseId,
            CourseName = input.CourseName,
            Number = input.Number,
            Name = input.Name,
            CurrentGroupId = input.CurrentGroupId,
            CurrentGroupName = input.CurrentGroupName,
            TargetGroupIds = targetGroups,
            TargetGroupNames = names,
            Decision = decision,
            DecisionCode = ToDecisionCode(decision),
            DecisionReason = reason,
            StudentCountsByTargetGroup = input.Downstream.StudentsByGroup,
            DownstreamClassifications = ClassifyDownstream(input),
            RequiresManualApproval = requiresApproval,
            MustNotModify = mustNotModify,
        };
    }
}
