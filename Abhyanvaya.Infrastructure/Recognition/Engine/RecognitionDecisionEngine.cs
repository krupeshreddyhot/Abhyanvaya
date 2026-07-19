using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

public sealed class RecognitionDecisionEngine : IRecognitionDecisionEngine
{
    public RecognitionDecision Decide(
        RecognitionDecisionContext context,
        IReadOnlySet<int>? alreadyAssignedStudentIds = null)
    {
        alreadyAssignedStudentIds ??= new HashSet<int>();
        var policy = context.Policy;
        var matches = context.RankedMatches;

        if (matches.Count == 0)
        {
            return new RecognitionDecision
            {
                DecisionType = RecognitionDecisionType.Unknown,
                Status = RecognitionStatus.Unknown,
                Confidence = 0,
                Distance = 1,
                Reason = "No similarity matches available.",
            };
        }

        var best = matches[0];
        var confidence = (decimal)Math.Clamp(best.NormalizedScore * 100f, 0f, 100f);
        var distance = (decimal)best.RawDistance;

        if (matches.Count > 1)
        {
            var second = matches[1];
            var scoreGap = best.NormalizedScore - second.NormalizedScore;
            if (scoreGap <= policy.TieThreshold)
            {
                return new RecognitionDecision
                {
                    DecisionType = RecognitionDecisionType.Tie,
                    Status = policy.ManualReviewEnabled
                        ? RecognitionStatus.LowConfidence
                        : RecognitionStatus.Unknown,
                    StudentId = null,
                    MatchedEmbeddingId = null,
                    Confidence = confidence,
                    Distance = distance,
                    Reason = "Top candidates within tie threshold.",
                    RequiresManualReview = policy.ManualReviewEnabled,
                };
            }
        }

        if (best.RawDistance > policy.LowConfidenceDistanceThreshold)
        {
            return new RecognitionDecision
            {
                DecisionType = RecognitionDecisionType.Unknown,
                Status = RecognitionStatus.Unknown,
                Confidence = confidence,
                Distance = distance,
                Reason = "Best match exceeds unknown threshold.",
            };
        }

        if (best.RawDistance > policy.MatchDistanceThreshold)
        {
            return new RecognitionDecision
            {
                DecisionType = RecognitionDecisionType.LowConfidence,
                Status = RecognitionStatus.LowConfidence,
                StudentId = best.StudentId,
                MatchedEmbeddingId = best.EmbeddingId,
                Confidence = confidence,
                Distance = distance,
                Reason = "Match within low-confidence band.",
                RequiresManualReview = policy.ManualReviewEnabled,
            };
        }

        if (alreadyAssignedStudentIds.Contains(best.StudentId))
        {
            return new RecognitionDecision
            {
                DecisionType = RecognitionDecisionType.Duplicate,
                Status = RecognitionStatus.Duplicate,
                StudentId = best.StudentId,
                MatchedEmbeddingId = best.EmbeddingId,
                Confidence = confidence,
                Distance = distance,
                Reason = "Student already assigned in this batch.",
            };
        }

        if ((float)confidence < policy.MinimumConfidence)
        {
            return new RecognitionDecision
            {
                DecisionType = RecognitionDecisionType.ManualReview,
                Status = RecognitionStatus.LowConfidence,
                StudentId = best.StudentId,
                MatchedEmbeddingId = best.EmbeddingId,
                Confidence = confidence,
                Distance = distance,
                Reason = "Confidence below minimum policy threshold.",
                RequiresManualReview = true,
            };
        }

        return new RecognitionDecision
        {
            DecisionType = RecognitionDecisionType.Recognized,
            Status = RecognitionStatus.Recognized,
            StudentId = best.StudentId,
            MatchedEmbeddingId = best.EmbeddingId,
            Confidence = confidence,
            Distance = distance,
        };
    }
}
