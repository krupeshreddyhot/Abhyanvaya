using Abhyanvaya.Application.ModelLifecycle;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Model lifecycle persistence — all SQL behind this repository (AI20.PHASE2.5).</summary>
public interface IModelLifecycleRepository
{
    Task<AiModelDefinition> AddModelDefinitionAsync(AiModelDefinition entity, CancellationToken cancellationToken = default);

    Task<AiModelVersion> AddModelVersionAsync(AiModelVersion entity, CancellationToken cancellationToken = default);

    Task<AiModelVersion?> GetModelVersionAsync(Guid modelVersionId, CancellationToken cancellationToken = default);

    Task<AiModelVersion?> GetActiveProductionVersionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiModelVersion>> ListVersionsAsync(Guid modelDefinitionId, CancellationToken cancellationToken = default);

    Task UpdateModelVersionAsync(AiModelVersion entity, CancellationToken cancellationToken = default);

    Task DeactivateAllVersionsAsync(Guid modelDefinitionId, CancellationToken cancellationToken = default);

    Task<GoldenDatasetDefinition> AddGoldenDatasetAsync(GoldenDatasetDefinition entity, CancellationToken cancellationToken = default);

    Task<GoldenDatasetDefinition?> GetGoldenDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoldenDatasetDefinition>> ListGoldenDatasetVersionsAsync(string datasetKey, CancellationToken cancellationToken = default);

    Task<ModelRolloutPlan> AddRolloutPlanAsync(ModelRolloutPlan entity, CancellationToken cancellationToken = default);

    Task AddAuditEntryAsync(ModelLifecycleAuditEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrainingCandidate>> ListRetrainingCandidatesAsync(int tenantId, CancellationToken cancellationToken = default);

    Task AddRetrainingCandidateAsync(RetrainingCandidate candidate, CancellationToken cancellationToken = default);
}
