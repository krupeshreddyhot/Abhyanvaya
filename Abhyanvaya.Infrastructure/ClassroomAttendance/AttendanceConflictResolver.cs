using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class HighestConfidenceConflictStrategy : IAttendanceConflictStrategy
{
    public string StrategyName => "HighestConfidence";

    public bool CanHandle(AttendanceConflictType conflictType) =>
        conflictType is AttendanceConflictType.DuplicateStudent
            or AttendanceConflictType.MultipleCandidates;

    public FaceRecognitionOutcome? Resolve(
        AttendanceConflict conflict,
        IReadOnlyList<FaceRecognitionOutcome> outcomes,
        IAttendancePolicy policy)
    {
        var candidates = outcomes
            .Where(o => o.RecognitionResult?.Decision?.Confidence != null)
            .OrderByDescending(o => o.RecognitionResult!.Decision!.Confidence)
            .ToList();

        return candidates.FirstOrDefault();
    }
}

public sealed class ManualReviewConflictStrategy : IAttendanceConflictStrategy
{
    public string StrategyName => "ManualReview";

    public bool CanHandle(AttendanceConflictType conflictType) =>
        conflictType is AttendanceConflictType.UnknownFace
            or AttendanceConflictType.BorderlineConfidence
            or AttendanceConflictType.DuplicateFace;

    public FaceRecognitionOutcome? Resolve(
        AttendanceConflict conflict,
        IReadOnlyList<FaceRecognitionOutcome> outcomes,
        IAttendancePolicy policy) =>
        outcomes.FirstOrDefault(o => o.FaceIndex == conflict.FaceIndex);
}

public sealed class AttendanceConflictResolver : IAttendanceConflictResolver
{
    private readonly IEnumerable<IAttendanceConflictStrategy> _strategies;

    public AttendanceConflictResolver(IEnumerable<IAttendanceConflictStrategy> strategies)
    {
        _strategies = strategies;
    }

    public AttendanceConflictResolutionResult Resolve(AttendanceSessionContext context)
    {
        var outcomes = (context.RecognitionOutcomes ?? Array.Empty<FaceRecognitionOutcome>()).ToList();
        var policy = context.Policy ?? throw new InvalidOperationException("Attendance policy is required for conflict resolution.");
        var conflicts = DetectConflicts(outcomes, policy);
        var resolvedConflicts = new List<AttendanceConflict>();

        foreach (var conflict in conflicts)
        {
            var strategy = _strategies.FirstOrDefault(s => s.CanHandle(conflict.ConflictType))
                ?? _strategies.First(s => s.StrategyName == "ManualReview");

            strategy.Resolve(conflict, outcomes, policy);
            resolvedConflicts.Add(conflict);
        }

        return new AttendanceConflictResolutionResult
        {
            ResolvedOutcomes = outcomes,
            ResolvedConflicts = resolvedConflicts,
        };
    }

    private static List<AttendanceConflict> DetectConflicts(
        IReadOnlyList<FaceRecognitionOutcome> outcomes,
        IAttendancePolicy policy)
    {
        var conflicts = new List<AttendanceConflict>();
        var assignedStudents = new HashSet<int>();

        foreach (var outcome in outcomes)
        {
            var decision = outcome.RecognitionResult?.Decision;
            if (decision == null)
            {
                conflicts.Add(new AttendanceConflict
                {
                    ConflictType = AttendanceConflictType.UnknownFace,
                    FaceIndex = outcome.FaceIndex,
                    Description = "Missing recognition decision.",
                });
                continue;
            }

            if (decision.Status == RecognitionStatus.Unknown)
            {
                conflicts.Add(new AttendanceConflict
                {
                    ConflictType = AttendanceConflictType.UnknownFace,
                    FaceIndex = outcome.FaceIndex,
                    Description = "Unknown face detected.",
                });
            }

            if (decision.Status == RecognitionStatus.LowConfidence)
            {
                conflicts.Add(new AttendanceConflict
                {
                    ConflictType = AttendanceConflictType.BorderlineConfidence,
                    FaceIndex = outcome.FaceIndex,
                    StudentId = decision.StudentId,
                    Description = "Borderline confidence match.",
                });
            }

            if (decision.StudentId.HasValue)
            {
                if (!policy.AllowDuplicateStudents && !assignedStudents.Add(decision.StudentId.Value))
                {
                    conflicts.Add(new AttendanceConflict
                    {
                        ConflictType = AttendanceConflictType.DuplicateStudent,
                        FaceIndex = outcome.FaceIndex,
                        StudentId = decision.StudentId,
                        Description = "Same student detected more than once.",
                    });
                }
            }

            if (decision.DecisionType == RecognitionDecisionType.Duplicate)
            {
                conflicts.Add(new AttendanceConflict
                {
                    ConflictType = AttendanceConflictType.DuplicateFace,
                    FaceIndex = outcome.FaceIndex,
                    StudentId = decision.StudentId,
                    Description = "Duplicate face assignment.",
                });
            }
        }

        return conflicts;
    }
}
