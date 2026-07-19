using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.ModelLifecycle;

public enum AIModelType
{
    Embedding = 0,
    Recognition = 1,
    Combined = 2,
}

public enum RolloutPolicyType
{
    Tenant = 0,
    Campus = 1,
    Department = 2,
    Percentage = 3,
    Canary = 4,
    FeatureFlag = 5,
}

public enum DriftSeverity
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public sealed record AIModelDescriptor
{
    public required Guid ModelId { get; init; }
    public required string Version { get; init; }
    public required AIModelType ModelType { get; init; }
    public required string EmbeddingVersion { get; init; }
    public required string RecognitionVersion { get; init; }
    public DateTime? TrainingDate { get; init; }
    public string? DatasetVersion { get; init; }
    public decimal? Accuracy { get; init; }
    public AIModelState Status { get; init; }
    public int? CreatedBy { get; init; }
    public required string Checksum { get; init; }
    public string? Signature { get; init; }
    public int PipelineVersion { get; init; }
    public required string ModelKey { get; init; }
}

public sealed record RegisterModelRequest
{
    public required string ModelKey { get; init; }
    public required AIModelType ModelType { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public int? CreatedBy { get; init; }
}

public sealed record CreateModelVersionRequest
{
    public required Guid ModelId { get; init; }
    public required string Version { get; init; }
    public required string EmbeddingVersion { get; init; }
    public required string RecognitionVersion { get; init; }
    public int PipelineVersion { get; init; } = 1;
    public DateTime? TrainingDate { get; init; }
    public string? DatasetVersion { get; init; }
    public decimal? Accuracy { get; init; }
    public required string Checksum { get; init; }
    public string? Signature { get; init; }
    public int? CreatedBy { get; init; }
}

public sealed record ModelCompatibilityResult
{
    public required bool IsCompatible { get; init; }
    public bool MigrationRequired { get; init; }
    public bool BackwardCompatible { get; init; }
    public IReadOnlyList<string>? Issues { get; init; }
}

public sealed record GoldenDatasetDescriptor
{
    public required Guid DatasetId { get; init; }
    public required string DatasetKey { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<GoldenDatasetSample> Samples { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public bool IsImmutable { get; init; } = true;
}

public sealed record GoldenDatasetSample
{
    public required string SampleId { get; init; }
    public required int ExpectedStudentId { get; init; }
    public required string ReferenceImagePath { get; init; }
    public string? ReferenceEmbeddingKey { get; init; }
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}

public sealed record RecognitionRegressionReport
{
    public required string DatasetId { get; init; }
    public required string DatasetVersion { get; init; }
    public required string ModelVersion { get; init; }
    public required int ExpectedCount { get; init; }
    public required int ActualCount { get; init; }
    public decimal Accuracy { get; init; }
    public int FalsePositives { get; init; }
    public int FalseNegatives { get; init; }
    public int Unknown { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public IReadOnlyList<RegressionComparisonEntry>? Comparisons { get; init; }
}

public sealed record RegressionComparisonEntry
{
    public required string SampleId { get; init; }
    public required int ExpectedStudentId { get; init; }
    public int? ActualStudentId { get; init; }
    public bool IsMatch { get; init; }
}

public sealed record RecognitionBenchmarkReport
{
    public required string BenchmarkId { get; init; }
    public required string ModelVersion { get; init; }
    public decimal Precision { get; init; }
    public decimal Recall { get; init; }
    public decimal FalseAcceptRate { get; init; }
    public decimal FalseRejectRate { get; init; }
    public decimal Top1Accuracy { get; init; }
    public decimal Top5Accuracy { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public TimeSpan P95Latency { get; init; }
    public long MemoryBytesPeak { get; init; }
    public decimal CpuUtilizationPercent { get; init; }
    public decimal? GpuUtilizationPercent { get; init; }
    public int ThroughputPerSecond { get; init; }
}

public sealed record RecognitionDriftReport
{
    public required Guid ModelId { get; init; }
    public required string ModelVersion { get; init; }
    public decimal CurrentAccuracy { get; init; }
    public decimal PreviousAccuracy { get; init; }
    public decimal ConfidenceShift { get; init; }
    public decimal EmbeddingDriftScore { get; init; }
    public decimal UnknownTrendPercent { get; init; }
    public decimal FalsePositiveTrendPercent { get; init; }
    public DriftSeverity Severity { get; init; }
    public string? Recommendation { get; init; }
}

public sealed record RecognitionQualitySummary
{
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }
    public required string PeriodLabel { get; init; }
    public decimal Accuracy { get; init; }
    public decimal Precision { get; init; }
    public decimal Recall { get; init; }
    public decimal ManualReviewPercent { get; init; }
    public decimal UnknownPercent { get; init; }
    public decimal TrendPercent { get; init; }
}

public sealed record RecognitionMetricsSnapshot
{
    public decimal RecognitionAccuracy { get; init; }
    public decimal AttendanceAccuracy { get; init; }
    public decimal Precision { get; init; }
    public decimal Recall { get; init; }
    public TimeSpan AverageLatency { get; init; }
    public decimal CpuUtilizationPercent { get; init; }
    public long MemoryBytes { get; init; }
    public int ThroughputPerMinute { get; init; }
    public int FailureCount { get; init; }
    public decimal UnknownPercent { get; init; }
    public decimal ManualReviewPercent { get; init; }
}

public sealed record RolloutRequest
{
    public required Guid ModelVersionId { get; init; }
    public required RolloutPolicyType PolicyType { get; init; }
    public int? TenantId { get; init; }
    public decimal? Percentage { get; init; }
    public bool IsCanary { get; init; }
}

public sealed record RolloutResult
{
    public required bool Success { get; init; }
    public Guid? RolloutId { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record RollbackRequest
{
    public required Guid ModelId { get; init; }
    public required string FromVersion { get; init; }
    public required string ToVersion { get; init; }
    public required string Reason { get; init; }
    public int? ActorUserId { get; init; }
    public bool Immediate { get; init; } = true;
}

public sealed record RollbackResult
{
    public required bool Success { get; init; }
    public AIModelDescriptor? RestoredModel { get; init; }
    public string? FailureReason { get; init; }
}

public sealed record RetrainingCandidate
{
    public required Guid CandidateId { get; init; }
    public required int TenantId { get; init; }
    public required int StudentId { get; init; }
    public required string Source { get; init; }
    public required string CorrectionType { get; init; }
    public DateTime QueuedUtc { get; init; }
}

public sealed record RunRegressionRequest
{
    public required Guid ModelVersionId { get; init; }
    public required Guid DatasetId { get; init; }
}

public sealed record RunBenchmarkRequest
{
    public required Guid ModelVersionId { get; init; }
    public required string BenchmarkId { get; init; }
    public int IterationCount { get; init; } = 100;
    public int CandidatePoolSize { get; init; } = 1000;
    public int TopK { get; init; } = 5;
}

public sealed record DriftDetectionRequest
{
    public required Guid ModelId { get; init; }
    public required string ModelVersion { get; init; }
    public decimal? PreviousAccuracy { get; init; }
}

public sealed record QualityAggregationRequest
{
    public required Guid ModelId { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

public sealed record QueueRetrainingCandidateRequest
{
    public required int StudentId { get; init; }
    public required int TenantId { get; init; }
    public required string CorrectionType { get; init; }
    public Guid? RecognitionId { get; init; }
}
