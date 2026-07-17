using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.ModelLifecycle;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ModelLifecycle.Persistence;

public sealed class ModelLifecycleRepository : IModelLifecycleRepository
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ModelLifecycleRepository> _logger;

    public ModelLifecycleRepository(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<ModelLifecycleRepository> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AiModelDefinition> AddModelDefinitionAsync(AiModelDefinition entity, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<AiModelVersion> AddModelVersionAsync(AiModelVersion entity, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<AiModelVersion?> GetModelVersionAsync(Guid modelVersionId, CancellationToken cancellationToken = default) =>
        _context.AiModelVersions
            .Include(v => v.ModelDefinition)
            .FirstOrDefaultAsync(v => v.Id == modelVersionId, cancellationToken);

    public Task<AiModelVersion?> GetActiveProductionVersionAsync(CancellationToken cancellationToken = default) =>
        _context.AiModelVersions
            .Include(v => v.ModelDefinition)
            .Where(v => v.IsActive && v.State == AIModelState.Production)
            .OrderByDescending(v => v.ActivatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AiModelVersion>> ListVersionsAsync(Guid modelDefinitionId, CancellationToken cancellationToken = default) =>
        await _context.AiModelVersions
            .Include(v => v.ModelDefinition)
            .Where(v => v.ModelDefinitionId == modelDefinitionId)
            .OrderByDescending(v => v.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task UpdateModelVersionAsync(AiModelVersion entity, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAllVersionsAsync(Guid modelDefinitionId, CancellationToken cancellationToken = default)
    {
        var versions = await _context.AiModelVersions
            .Where(v => v.ModelDefinitionId == modelDefinitionId && v.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var version in versions)
        {
            version.IsActive = false;
        }

        if (versions.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<GoldenDatasetDefinition> AddGoldenDatasetAsync(GoldenDatasetDefinition entity, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<GoldenDatasetDefinition?> GetGoldenDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default) =>
        _context.GoldenDatasetDefinitions.FirstOrDefaultAsync(d => d.Id == datasetId, cancellationToken);

    public async Task<IReadOnlyList<GoldenDatasetDefinition>> ListGoldenDatasetVersionsAsync(string datasetKey, CancellationToken cancellationToken = default) =>
        await _context.GoldenDatasetDefinitions
            .Where(d => d.DatasetKey == datasetKey)
            .OrderByDescending(d => d.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task<ModelRolloutPlan> AddRolloutPlanAsync(ModelRolloutPlan entity, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task AddAuditEntryAsync(ModelLifecycleAuditEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetrainingCandidate>> ListRetrainingCandidatesAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var entries = await _context.RetrainingCandidateEntries
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.QueuedUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(e => new RetrainingCandidate
        {
            CandidateId = e.Id,
            TenantId = e.TenantId,
            StudentId = e.StudentId,
            Source = e.Source,
            CorrectionType = e.CorrectionType,
            QueuedUtc = e.QueuedUtc,
        }).ToList();
    }

    public async Task AddRetrainingCandidateAsync(RetrainingCandidate candidate, CancellationToken cancellationToken = default)
    {
        var entry = new RetrainingCandidateEntry
        {
            Id = candidate.CandidateId == Guid.Empty ? Guid.NewGuid() : candidate.CandidateId,
            TenantId = candidate.TenantId,
            StudentId = candidate.StudentId,
            Source = candidate.Source,
            CorrectionType = candidate.CorrectionType,
            QueuedUtc = candidate.QueuedUtc,
        };

        await _context.AddAsync(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public static class ModelLifecycleMapper
{
    public static AIModelDescriptor ToDescriptor(AiModelVersion version) =>
        new()
        {
            ModelId = version.ModelDefinitionId,
            Version = version.Version,
            ModelType = Enum.TryParse<AIModelType>(version.ModelDefinition.ModelType, out var t) ? t : AIModelType.Combined,
            EmbeddingVersion = version.EmbeddingVersion,
            RecognitionVersion = version.RecognitionVersion,
            TrainingDate = version.TrainingDateUtc,
            DatasetVersion = version.DatasetVersion,
            Accuracy = version.Accuracy,
            Status = version.State,
            CreatedBy = version.CreatedBy,
            Checksum = version.Checksum,
            Signature = version.Signature,
            PipelineVersion = version.PipelineVersion,
            ModelKey = version.ModelDefinition.ModelKey,
        };

    public static GoldenDatasetDescriptor ToDescriptor(GoldenDatasetDefinition entity)
    {
        var samples = JsonSerializer.Deserialize<List<GoldenDatasetSample>>(entity.SamplesJson) ?? [];
        var metadata = string.IsNullOrEmpty(entity.MetadataJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson);

        return new GoldenDatasetDescriptor
        {
            DatasetId = entity.Id,
            DatasetKey = entity.DatasetKey,
            Version = entity.Version,
            Name = entity.Name,
            Samples = samples,
            Metadata = metadata,
            IsImmutable = entity.IsImmutable,
        };
    }
}
