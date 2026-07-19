using Abhyanvaya.Application.Recognition.Pipeline;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Recognition;

public static class RecognitionFailureCodes
{
    public const string StageFailed = "recognition.stage_failed";
    public const string Cancelled = "recognition.cancelled";
    public const string NoCandidates = "recognition.no_candidates";
    public const string NoEmbedding = "recognition.no_embedding";
    public const string UnexpectedFailure = "recognition.unexpected_failure";
}

public enum SimilarityMetric
{
    Cosine = 0,
    Euclidean = 1,
    InnerProduct = 2,
}

public enum RecognitionDecisionType
{
    Recognized = 0,
    Unknown = 1,
    LowConfidence = 2,
    Duplicate = 3,
    ManualReview = 4,
    Tie = 5,
}

public enum RecognitionCandidateScope
{
    Tenant = 0,
    Course = 1,
    Section = 2,
    Class = 3,
    Building = 4,
    Camera = 5,
    AttendanceSession = 6,
}

public sealed record RecognitionCandidateFilter
{
    public required int TenantId { get; init; }
    public RecognitionCandidateScope Scope { get; init; } = RecognitionCandidateScope.AttendanceSession;
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? SemesterId { get; init; }
    public int? BuildingId { get; init; }
    public string? CameraId { get; init; }
    public Guid? AttendanceSessionId { get; init; }
    public DateTime? TimeWindowStartUtc { get; init; }
    public DateTime? TimeWindowEndUtc { get; init; }
}

public sealed record RecognitionCandidate
{
    public required int StudentId { get; init; }
    public required Guid EmbeddingId { get; init; }
    public required float[] EmbeddingVector { get; init; }
    public required long PhotoVersion { get; init; }
    public required string EmbeddingModel { get; init; }
    public required string EmbeddingVersion { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record RecognitionSearchResult
{
    public required int StudentId { get; init; }
    public required Guid EmbeddingId { get; init; }
    public required float SimilarityScore { get; init; }
    public required int Rank { get; init; }
    public required float Distance { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

public sealed record SimilarityMatch
{
    public required int StudentId { get; init; }
    public required Guid EmbeddingId { get; init; }
    public required float NormalizedScore { get; init; }
    public required float RawDistance { get; init; }
    public required int Rank { get; init; }
}

public sealed record SimilarityStatistics
{
    public float BestScore { get; init; }
    public float WorstScore { get; init; }
    public float MeanScore { get; init; }
    public float ScoreSpread { get; init; }
    public int MatchCount { get; init; }
}

public sealed record RecognitionDecision
{
    public required RecognitionDecisionType DecisionType { get; init; }
    public required RecognitionStatus Status { get; init; }
    public int? StudentId { get; init; }
    public Guid? MatchedEmbeddingId { get; init; }
    public decimal Confidence { get; init; }
    public decimal Distance { get; init; }
    public string? Reason { get; init; }
    public bool RequiresManualReview { get; init; }
}

public sealed record RecognitionDecisionContext
{
    public required IReadOnlyList<SimilarityMatch> RankedMatches { get; init; }
    public required IReadOnlyList<RecognitionCandidate> Candidates { get; init; }
    public required IRecognitionPolicy Policy { get; init; }
    public required SimilarityStatistics Statistics { get; init; }
    public IReadOnlyDictionary<string, object>? Telemetry { get; init; }
}

public sealed record RecognitionStatistics
{
    public TimeSpan SearchDuration { get; init; }
    public TimeSpan RankingDuration { get; init; }
    public TimeSpan SimilarityDuration { get; init; }
    public TimeSpan DecisionDuration { get; init; }
    public TimeSpan PersistenceDuration { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int TopK { get; init; }
    public int CandidateCount { get; init; }
    public IReadOnlyDictionary<string, TimeSpan>? StageDurations { get; init; }
}

public sealed record RecognitionRequestContext
{
    public required Guid RecognitionRequestId { get; init; }
    public required Guid AttendanceSessionId { get; init; }
    public required int TenantId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required int PipelineVersion { get; init; }
    public required int FaceIndex { get; init; }
    public short ImageSequence { get; init; } = 1;
    public RecognitionPipelineState PipelineState { get; init; } = RecognitionPipelineState.Pending;
    public bool CancellationRequested { get; init; }
}

public sealed record RecognitionPipelineRequest
{
    public required RecognitionRequestContext Context { get; init; }
    public byte[]? ImageBytes { get; init; }
    public float[]? QueryEmbedding { get; init; }
    public required RecognitionCandidateFilter CandidateFilter { get; init; }
    public int TopK { get; init; } = 10;
    public SimilarityMetric SimilarityMetric { get; init; } = SimilarityMetric.Cosine;
    public int BoundingBoxX { get; init; }
    public int BoundingBoxY { get; init; }
    public int BoundingBoxWidth { get; init; }
    public int BoundingBoxHeight { get; init; }
    public string? FaceImageKey { get; init; }
    public IReadOnlySet<int>? AlreadyAssignedStudentIds { get; init; }
}

public sealed record RecognitionPipelineStageOutcome
{
    public required RecognitionPipelineStage? ManifestStage { get; init; }
    public required string StageName { get; init; }
    public required bool Success { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record RecognitionResult
{
    public required bool Success { get; init; }
    public required Guid RecognitionRequestId { get; init; }
    public required Guid AttendanceSessionId { get; init; }
    public required int TenantId { get; init; }
    public required int FaceIndex { get; init; }
    public RecognitionPipelineState Status { get; init; }
    public RecognitionDecision? Decision { get; init; }
    public Guid? PersistedRecognitionId { get; init; }
    public IReadOnlyList<RecognitionSearchResult>? TopCandidates { get; init; }
    public RecognitionStatistics? Statistics { get; init; }
    public IReadOnlyList<string>? Warnings { get; init; }
    public IReadOnlyDictionary<string, object>? Telemetry { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record RecognitionPersistenceRequest
{
    public required RecognitionRequestContext Context { get; init; }
    public required RecognitionDecision Decision { get; init; }
    public required IReadOnlyList<RecognitionSearchResult> TopCandidates { get; init; }
    public required RecognitionStatistics Statistics { get; init; }
    public int BoundingBoxX { get; init; }
    public int BoundingBoxY { get; init; }
    public int BoundingBoxWidth { get; init; }
    public int BoundingBoxHeight { get; init; }
    public string? FaceImageKey { get; init; }
}

public sealed record RecognitionPersistenceResult
{
    public required bool Success { get; init; }
    public Guid? RecognitionId { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record VectorSearchRequest
{
    public required float[] QueryEmbedding { get; init; }
    public required IReadOnlyList<RecognitionCandidate> Candidates { get; init; }
    public required int TopK { get; init; }
    public SimilarityMetric Metric { get; init; } = SimilarityMetric.Cosine;
}

public sealed record VectorSearchResponse
{
    public required IReadOnlyList<RecognitionSearchResult> Results { get; init; }
    public required TimeSpan Duration { get; init; }
    public int CandidatesSearched { get; init; }
}

public interface IRecognitionPolicy
{
    float MinimumConfidence { get; }
    float UnknownThreshold { get; }
    float TieThreshold { get; }
    int MaximumCandidates { get; }
    bool AutoAccept { get; }
    bool ManualReviewEnabled { get; }
    float MatchDistanceThreshold { get; }
    float LowConfidenceDistanceThreshold { get; }
}
