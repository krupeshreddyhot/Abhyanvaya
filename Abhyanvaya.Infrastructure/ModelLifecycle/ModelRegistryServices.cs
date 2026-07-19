using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.ModelLifecycle;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.ModelLifecycle.Persistence;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ModelLifecycle;

public sealed class ModelRegistry : IModelRegistry
{
    private readonly IModelLifecycleRepository _repository;
    private readonly ILogger<ModelRegistry> _logger;

    public ModelRegistry(IModelLifecycleRepository repository, ILogger<ModelRegistry> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AIModelDescriptor> RegisterModelAsync(RegisterModelRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new AiModelDefinition
        {
            Id = Guid.NewGuid(),
            ModelKey = request.ModelKey,
            ModelType = request.ModelType.ToString(),
            DisplayName = request.DisplayName,
            Description = request.Description,
            CreatedBy = request.CreatedBy,
            CreatedUtc = DateTime.UtcNow,
        };

        await _repository.AddModelDefinitionAsync(entity, cancellationToken);

        _logger.LogInformation("Model registered. ModelKey={ModelKey} ModelId={ModelId}", request.ModelKey, entity.Id);

        return new AIModelDescriptor
        {
            ModelId = entity.Id,
            Version = "0.0.0",
            ModelType = request.ModelType,
            EmbeddingVersion = string.Empty,
            RecognitionVersion = string.Empty,
            Status = AIModelState.Draft,
            Checksum = string.Empty,
            ModelKey = entity.ModelKey,
        };
    }

    public async Task<AIModelDescriptor?> GetModelAsync(Guid modelId, string version, CancellationToken cancellationToken = default)
    {
        var versions = await _repository.ListVersionsAsync(modelId, cancellationToken);
        var match = versions.FirstOrDefault(v => v.Version == version);
        return match == null ? null : ModelLifecycleMapper.ToDescriptor(match);
    }

    public async Task<IReadOnlyList<AIModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var active = await _repository.GetActiveProductionVersionAsync(cancellationToken);
        return active == null
            ? Array.Empty<AIModelDescriptor>()
            : new[] { ModelLifecycleMapper.ToDescriptor(active) };
    }
}

public sealed class ModelVersionManager : IModelVersionManager
{
    private readonly IModelLifecycleRepository _repository;
    private readonly IModelCompatibilityService _compatibilityService;
    private readonly ILogger<ModelVersionManager> _logger;

    public ModelVersionManager(
        IModelLifecycleRepository repository,
        IModelCompatibilityService compatibilityService,
        ILogger<ModelVersionManager> logger)
    {
        _repository = repository;
        _compatibilityService = compatibilityService;
        _logger = logger;
    }

    public async Task<AIModelDescriptor> CreateVersionAsync(CreateModelVersionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new AiModelVersion
        {
            Id = Guid.NewGuid(),
            ModelDefinitionId = request.ModelId,
            Version = request.Version,
            State = AIModelState.Draft,
            EmbeddingVersion = request.EmbeddingVersion,
            RecognitionVersion = request.RecognitionVersion,
            PipelineVersion = request.PipelineVersion,
            TrainingDateUtc = request.TrainingDate,
            DatasetVersion = request.DatasetVersion,
            Accuracy = request.Accuracy,
            Checksum = request.Checksum,
            Signature = request.Signature,
            CreatedBy = request.CreatedBy,
            CreatedUtc = DateTime.UtcNow,
        };

        await _repository.AddModelVersionAsync(entity, cancellationToken);
        var loaded = await _repository.GetModelVersionAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("Failed to load created model version.");

        return ModelLifecycleMapper.ToDescriptor(loaded);
    }

    public async Task<AIModelDescriptor> ActivateVersionAsync(Guid modelVersionId, AIModelState targetState, CancellationToken cancellationToken = default)
    {
        var version = await _repository.GetModelVersionAsync(modelVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Model version '{modelVersionId}' not found.");

        var descriptor = ModelLifecycleMapper.ToDescriptor(version);
        var compatibility = _compatibilityService.Validate(descriptor, version.PipelineVersion);
        if (!compatibility.IsCompatible)
        {
            throw new InvalidOperationException(string.Join("; ", compatibility.Issues ?? Array.Empty<string>()));
        }

        if (targetState == AIModelState.Production)
        {
            await _repository.DeactivateAllVersionsAsync(version.ModelDefinitionId, cancellationToken);
        }

        version.State = targetState;
        version.IsActive = targetState is AIModelState.Production or AIModelState.Canary;
        version.ActivatedUtc = DateTime.UtcNow;
        await _repository.UpdateModelVersionAsync(version, cancellationToken);

        _logger.LogInformation(
            "Model activated. ModelId={ModelId} Version={Version} State={State}",
            version.ModelDefinitionId,
            version.Version,
            targetState);

        return ModelLifecycleMapper.ToDescriptor(version);
    }

    public async Task<AIModelDescriptor> RetireVersionAsync(Guid modelVersionId, string reason, CancellationToken cancellationToken = default)
    {
        var version = await _repository.GetModelVersionAsync(modelVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Model version '{modelVersionId}' not found.");

        version.State = AIModelState.Retired;
        version.IsActive = false;
        version.RetiredUtc = DateTime.UtcNow;
        await _repository.UpdateModelVersionAsync(version, cancellationToken);

        await _repository.AddAuditEntryAsync(new ModelLifecycleAuditEntry
        {
            Id = Guid.NewGuid(),
            ModelDefinitionId = version.ModelDefinitionId,
            Action = "Retire",
            FromVersion = version.Version,
            Reason = reason,
            OccurredUtc = DateTime.UtcNow,
        }, cancellationToken);

        _logger.LogInformation("Model retired. Version={Version} Reason={Reason}", version.Version, reason);

        return ModelLifecycleMapper.ToDescriptor(version);
    }

    public async Task<AIModelDescriptor> DeprecateVersionAsync(Guid modelVersionId, CancellationToken cancellationToken = default)
    {
        var version = await _repository.GetModelVersionAsync(modelVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Model version '{modelVersionId}' not found.");

        version.State = AIModelState.Deprecated;
        await _repository.UpdateModelVersionAsync(version, cancellationToken);
        return ModelLifecycleMapper.ToDescriptor(version);
    }
}

public sealed class ActiveModelProvider : IActiveModelProvider
{
    private readonly IModelLifecycleRepository _repository;

    public ActiveModelProvider(IModelLifecycleRepository repository)
    {
        _repository = repository;
    }

    public async Task<AIModelDescriptor?> GetActiveModelAsync(CancellationToken cancellationToken = default)
    {
        var version = await _repository.GetActiveProductionVersionAsync(cancellationToken);
        return version == null ? null : ModelLifecycleMapper.ToDescriptor(version);
    }

    public Task<AIModelDescriptor?> GetActiveModelForTenantAsync(int tenantId, CancellationToken cancellationToken = default) =>
        GetActiveModelAsync(cancellationToken);
}
