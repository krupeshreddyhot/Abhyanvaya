using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Enrollment.Persistence;

public static class EnrollmentPersistenceFailureCodes
{
    public const string MissingEnrollment = "persistence.missing_enrollment";
    public const string DuplicateEmbedding = "persistence.duplicate_embedding";
    public const string ValidationMismatch = "persistence.validation_mismatch";
    public const string ConcurrencyConflict = "persistence.concurrency_conflict";
    public const string PolicyRejected = "persistence.policy_rejected";
    public const string DatabaseFailure = "persistence.database_failure";
}

public static class EnrollmentPersistenceState
{
    public const string EmbeddingPending = "EmbeddingPending";
    public const string EmbeddingGenerated = "EmbeddingGenerated";
    public const string Persisted = "Persisted";
    public const string ReadyForRecognition = "ReadyForRecognition";
}

public sealed record EnrollmentPersistenceRequest
{
    public required EnrollmentEmbeddingArtifact Artifact { get; init; }

    public EmbeddingMetadata? Metadata { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}

public sealed record EnrollmentPersistenceResult
{
    public required bool Success { get; init; }

    public int StudentId { get; init; }

    public Guid BatchId { get; init; }

    public Guid? EmbeddingId { get; init; }

    public EnrollmentStatus? Status { get; init; }

    public string? PersistenceState { get; init; }

    public DateTimeOffset? PersistedUtc { get; init; }

    public TimeSpan Duration { get; init; }

    public Guid CorrelationId { get; init; }

    public int PipelineVersion { get; init; }

    public int ValidationVersion { get; init; }

    public int StorageVersion { get; init; }

    public int ManifestVersion { get; init; }

    public int ArtifactVersion { get; init; }

    public string? EmbeddingModelVersion { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }

    public EnrollmentPersistenceStatistics? Statistics { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureReason { get; init; }

    public bool IsDuplicate { get; init; }

    public static EnrollmentPersistenceResult Succeeded(
        int studentId,
        Guid batchId,
        Guid embeddingId,
        EnrollmentStatus status,
        string persistenceState,
        DateTimeOffset persistedUtc,
        TimeSpan duration,
        EnrollmentEmbeddingArtifact artifact,
        IReadOnlyList<string>? warnings,
        EnrollmentPersistenceStatistics statistics,
        bool isDuplicate = false) =>
        new()
        {
            Success = true,
            StudentId = studentId,
            BatchId = batchId,
            EmbeddingId = embeddingId,
            Status = status,
            PersistenceState = persistenceState,
            PersistedUtc = persistedUtc,
            Duration = duration,
            CorrelationId = artifact.CorrelationId,
            PipelineVersion = artifact.PipelineVersion,
            ValidationVersion = artifact.ValidationVersion,
            StorageVersion = artifact.StorageVersion,
            ManifestVersion = artifact.ManifestVersion,
            ArtifactVersion = artifact.ArtifactVersion,
            EmbeddingModelVersion = artifact.EmbeddingModelVersion,
            Warnings = warnings,
            Statistics = statistics,
            IsDuplicate = isDuplicate,
        };

    public static EnrollmentPersistenceResult Failed(
        EnrollmentEmbeddingArtifact artifact,
        TimeSpan duration,
        string code,
        string reason) =>
        new()
        {
            Success = false,
            StudentId = artifact.StudentId,
            BatchId = artifact.BatchId,
            Duration = duration,
            CorrelationId = artifact.CorrelationId,
            PipelineVersion = artifact.PipelineVersion,
            ValidationVersion = artifact.ValidationVersion,
            StorageVersion = artifact.StorageVersion,
            ManifestVersion = artifact.ManifestVersion,
            ArtifactVersion = artifact.ArtifactVersion,
            FailureCode = code,
            FailureReason = reason,
        };
}

public sealed record EnrollmentPersistenceStatistics
{
    public required TimeSpan WriteDuration { get; init; }

    public required TimeSpan DatabaseDuration { get; init; }

    public required TimeSpan TransactionDuration { get; init; }

    public int RowsInserted { get; init; }

    public int RowsUpdated { get; init; }

    public int RetryCount { get; init; }

    public IReadOnlyList<string>? Warnings { get; init; }
}

public sealed record EnrollmentPersistencePolicyContext
{
    public required Guid ItemId { get; init; }

    public required int StudentId { get; init; }

    public required Guid BatchId { get; init; }

    public required EnrollmentStatus CurrentStatus { get; init; }

    public Guid? ExistingEmbeddingId { get; init; }

    public string? ExistingEmbeddingVersion { get; init; }

    public required string RequestedEmbeddingVersion { get; init; }

    public int PipelineVersion { get; init; }
}

public sealed record EnrollmentPersistencePolicyDecision
{
    public bool AllowPersist { get; init; } = true;

    public bool AllowOverwrite { get; init; }

    public bool ReturnExistingOnDuplicate { get; init; } = true;

    public bool KeepHistoricalVersions { get; init; } = true;

    public bool StoreFailedEmbeddings { get; init; }

    public string? RejectionReason { get; init; }
}

public sealed record EnrollmentDuplicateDetectionRequest
{
    public required Guid ItemId { get; init; }

    public required int StudentId { get; init; }

    public required Guid BatchId { get; init; }

    public required string EmbeddingModel { get; init; }

    public required string EmbeddingModelVersion { get; init; }

    public required int PipelineVersion { get; init; }

    public Guid? ExistingEmbeddingId { get; init; }

    public EnrollmentStatus ItemStatus { get; init; }

    public string? ItemEmbeddingVersion { get; init; }
}

public sealed record EnrollmentDuplicateDetectionResult
{
    public required bool IsDuplicate { get; init; }

    public Guid? ExistingEmbeddingId { get; init; }

    public string? Reason { get; init; }
}

public sealed record EnrollmentPersistenceWriteRequest
{
    public required StudentEnrollmentItem Item { get; init; }

    public required StudentEnrollmentBatch Batch { get; init; }

    public required Student Student { get; init; }

    public required EnrollmentEmbeddingArtifact Artifact { get; init; }

    public EmbeddingMetadata? Metadata { get; init; }

    public required EnrollmentPersistenceAudit Audit { get; init; }

    public required int? CreatedByUserId { get; init; }

    public required DateTime PersistedUtc { get; init; }

    public required bool KeepHistoricalVersions { get; init; }
}

public sealed record EnrollmentPersistenceWriteOutcome
{
    public required Guid EmbeddingId { get; init; }

    public required int RowsInserted { get; init; }

    public required int RowsUpdated { get; init; }
}
