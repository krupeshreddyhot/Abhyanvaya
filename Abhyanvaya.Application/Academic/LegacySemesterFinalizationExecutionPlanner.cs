using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3E —
/// Fail-closed planner: maps 3D classifier dispositions to execution actions.
/// Never invents GroupId. Never auto-maps SAFE_SINGLE_GROUP without explicit Architect approval flag.
/// </summary>
public static class LegacySemesterFinalizationExecutionPlanner
{
    public const string PromptCode = "P1-4-3E";

    public sealed record PlanInput(
        int SemesterId,
        int Number,
        string Name,
        int CourseId,
        int? CurrentGroupId,
        LegacySemesterFinalizationDisposition ClassifierDisposition,
        string ClassifierEvidence,
        bool HasOpenRetainJournal,
        bool AllowSafeSingleGroupFinalization,
        int? ApprovedSingleGroupId,
        IReadOnlyList<int> TeachingGroupIds,
        int? CandidateTgTargetSemesterId);

    public sealed record PlanResult(
        string DispositionCode,
        string Action,
        string BlockingReason,
        bool MutationAllowed,
        bool WriteRetainJournal,
        bool AssignGroupId,
        int? AssignedGroupId);

    public static PlanResult Plan(PlanInput input)
    {
        if (input.CurrentGroupId is > 0)
        {
            return new PlanResult(
                LegacySemesterExecutionDispositionCodes.AlreadyGroupSpecific,
                "Skip",
                "Semester already Group-specific; legacy finalization must not alter it.",
                MutationAllowed: false,
                WriteRetainJournal: false,
                AssignGroupId: false,
                AssignedGroupId: null);
        }

        if (input.ClassifierDisposition == LegacySemesterFinalizationDisposition.BlockedByTeachingGroupReference)
        {
            return new PlanResult(
                LegacySemesterExecutionDispositionCodes.BlockedByTeachingGroupReference,
                "DeferTg",
                "Teaching Group residual — OUT OF SCOPE for Prompt 3E; separate TG remediation required.",
                MutationAllowed: false,
                WriteRetainJournal: false,
                AssignGroupId: false,
                AssignedGroupId: null);
        }

        if (input.ClassifierDisposition == LegacySemesterFinalizationDisposition.DuplicateReview)
        {
            return new PlanResult(
                LegacySemesterExecutionDispositionCodes.DuplicateReview,
                "Block",
                "DUPLICATE_REVIEW — no automatic merge/delete/Group assignment.",
                MutationAllowed: false,
                WriteRetainJournal: false,
                AssignGroupId: false,
                AssignedGroupId: null);
        }

        if (input.ClassifierDisposition == LegacySemesterFinalizationDisposition.ManualMappingRequired
            || input.ClassifierDisposition == LegacySemesterFinalizationDisposition.SplitRequired
            || input.ClassifierDisposition == LegacySemesterFinalizationDisposition.UnknownRequiresArchitectDecision)
        {
            return new PlanResult(
                LegacySemesterExecutionDispositionCodes.ManualMappingRequired,
                "Block",
                "MANUAL_MAPPING_REQUIRED / ambiguous — fail closed; no mutation.",
                MutationAllowed: false,
                WriteRetainJournal: false,
                AssignGroupId: false,
                AssignedGroupId: null);
        }

        if (input.ClassifierDisposition == LegacySemesterFinalizationDisposition.HistoricalRetain)
        {
            if (input.HasOpenRetainJournal)
            {
                return new PlanResult(
                    LegacySemesterExecutionDispositionCodes.RetainHistorical,
                    "AlreadyComplete",
                    "RETAIN_HISTORICAL already journaled; zero writes.",
                    MutationAllowed: false,
                    WriteRetainJournal: false,
                    AssignGroupId: false,
                    AssignedGroupId: null);
            }

            // Architect-approved in Prompt 3E: empty HISTORICAL_RETAIN rows may be journaled as retained.
            return new PlanResult(
                LegacySemesterExecutionDispositionCodes.RetainHistorical,
                "Retain",
                "",
                MutationAllowed: true,
                WriteRetainJournal: true,
                AssignGroupId: false,
                AssignedGroupId: null);
        }

        if (input.ClassifierDisposition == LegacySemesterFinalizationDisposition.SafeSingleGroupMapping)
        {
            // CRITICAL: never convert NULL-group to Group merely because one Group exists.
            // Only when AllowSafeSingleGroupFinalization + ApprovedSingleGroupId are explicitly set.
            if (!input.AllowSafeSingleGroupFinalization || input.ApprovedSingleGroupId is not > 0)
            {
                return new PlanResult(
                    LegacySemesterExecutionDispositionCodes.ManualMappingRequired,
                    "Block",
                    "SAFE_SINGLE_GROUP_MAPPING is not auto-executed without explicit Architect approval flag.",
                    MutationAllowed: false,
                    WriteRetainJournal: false,
                    AssignGroupId: false,
                    AssignedGroupId: null);
            }

            if (input.HasOpenRetainJournal)
            {
                return new PlanResult(
                    LegacySemesterExecutionDispositionCodes.FinalizedLegacy,
                    "AlreadyComplete",
                    "FINALIZED_LEGACY already journaled; zero writes.",
                    MutationAllowed: false,
                    WriteRetainJournal: false,
                    AssignGroupId: false,
                    AssignedGroupId: null);
            }

            return new PlanResult(
                LegacySemesterExecutionDispositionCodes.FinalizedLegacy,
                "Finalize",
                "",
                MutationAllowed: true,
                WriteRetainJournal: true,
                AssignGroupId: true,
                AssignedGroupId: input.ApprovedSingleGroupId);
        }

        return new PlanResult(
            LegacySemesterExecutionDispositionCodes.ManualMappingRequired,
            "Block",
            $"Unsupported classifier disposition {input.ClassifierDisposition}; fail closed.",
            MutationAllowed: false,
            WriteRetainJournal: false,
            AssignGroupId: false,
            AssignedGroupId: null);
    }
}
