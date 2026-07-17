using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>Registered AI model family (AI20.PHASE2.5).</summary>
public class AiModelDefinition
{
    public Guid Id { get; set; }

    public required string ModelKey { get; set; }

    public required string ModelType { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedUtc { get; set; }

    public int? CreatedBy { get; set; }

    public ICollection<AiModelVersion> Versions { get; set; } = new List<AiModelVersion>();
}

/// <summary>Immutable version snapshot for an AI model (AI20.PHASE2.5).</summary>
public class AiModelVersion
{
    public Guid Id { get; set; }

    public Guid ModelDefinitionId { get; set; }

    public required string Version { get; set; }

    public AIModelState State { get; set; } = AIModelState.Draft;

    public required string EmbeddingVersion { get; set; }

    public required string RecognitionVersion { get; set; }

    public int PipelineVersion { get; set; }

    public DateTime? TrainingDateUtc { get; set; }

    public string? DatasetVersion { get; set; }

    public decimal? Accuracy { get; set; }

    public required string Checksum { get; set; }

    public string? Signature { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? ActivatedUtc { get; set; }

    public DateTime? RetiredUtc { get; set; }

    public bool IsActive { get; set; }

    public AiModelDefinition ModelDefinition { get; set; } = null!;
}

/// <summary>Versioned golden dataset metadata (AI20.PHASE2.5).</summary>
public class GoldenDatasetDefinition
{
    public Guid Id { get; set; }

    public required string DatasetKey { get; set; }

    public required string Version { get; set; }

    public required string Name { get; set; }

    public required string SamplesJson { get; set; }

    public string? MetadataJson { get; set; }

    public DateTime CreatedUtc { get; set; }

    public int? CreatedBy { get; set; }

    public bool IsImmutable { get; set; } = true;
}

/// <summary>Model rollout plan (AI20.PHASE2.5).</summary>
public class ModelRolloutPlan
{
    public Guid Id { get; set; }

    public Guid ModelVersionId { get; set; }

    public required string RolloutKey { get; set; }

    public required string PolicyType { get; set; }

    public int? TenantId { get; set; }

    public decimal? Percentage { get; set; }

    public bool IsCanary { get; set; }

    public AIModelState TargetState { get; set; } = AIModelState.Canary;

    public DateTime StartedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public string? Status { get; set; }

    public AiModelVersion ModelVersion { get; set; } = null!;
}

/// <summary>Audit trail for rollbacks and lifecycle actions (AI20.PHASE2.5).</summary>
public class ModelLifecycleAuditEntry
{
    public Guid Id { get; set; }

    public Guid ModelDefinitionId { get; set; }

    public required string Action { get; set; }

    public required string FromVersion { get; set; }

    public string? ToVersion { get; set; }

    public required string Reason { get; set; }

    public int? ActorUserId { get; set; }

    public DateTime OccurredUtc { get; set; }
}

/// <summary>Queued retraining candidate from teacher corrections (AI20.PHASE2.5).</summary>
public class RetrainingCandidateEntry
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public int StudentId { get; set; }

    public required string Source { get; set; }

    public required string CorrectionType { get; set; }

    public Guid? RecognitionId { get; set; }

    public DateTime QueuedUtc { get; set; }
}
