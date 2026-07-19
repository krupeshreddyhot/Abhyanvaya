using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.ModelLifecycle;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.ModelLifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.ModelLifecycle;

public sealed class ModelLifecycleFrameworkTests
{
    private readonly Mock<IModelLifecycleRepository> _repository = new();
    private readonly ModelLifecycleOptions _options = new()
    {
        SupportedPipelineVersion = 1,
        DefaultEmbeddingVersion = "insightface",
        DefaultRecognitionVersion = "insightface",
    };

    [Fact]
    public void CompatibilityService_ApprovesMatchingVersions()
    {
        var service = new EmbeddingCompatibilityService(Options.Create(_options));
        var result = service.CheckCompatibility("insightface-1.0", "insightface-1.0", 1);

        Assert.True(result.IsCompatible);
        Assert.True(result.BackwardCompatible);
    }

    [Fact]
    public void CompatibilityService_FlagsUnsupportedPipeline()
    {
        var service = new EmbeddingCompatibilityService(Options.Create(_options));
        var result = service.CheckCompatibility("insightface-1.0", "insightface-1.0", 99);

        Assert.False(result.IsCompatible);
        Assert.NotNull(result.Issues);
    }

    [Fact]
    public async Task ActiveModelProvider_ReturnsProductionModel()
    {
        var modelId = Guid.NewGuid();
        var version = CreateVersionEntity(modelId, "1.0.0", AIModelState.Production, true);

        _repository.Setup(r => r.GetActiveProductionVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var provider = new ActiveModelProvider(_repository.Object);
        var active = await provider.GetActiveModelAsync();

        Assert.NotNull(active);
        Assert.Equal("1.0.0", active!.Version);
        Assert.Equal(AIModelState.Production, active.Status);
    }

    [Fact]
    public async Task VersionManager_ActivatesVersion_WhenCompatible()
    {
        var modelId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var version = CreateVersionEntity(modelId, "1.0.0", AIModelState.Draft, false);
        version.Id = versionId;

        _repository.Setup(r => r.GetModelVersionAsync(versionId, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _repository.Setup(r => r.DeactivateAllVersionsAsync(modelId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repository.Setup(r => r.UpdateModelVersionAsync(It.IsAny<Domain.Entities.AiModelVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var manager = new ModelVersionManager(
            _repository.Object,
            new ModelCompatibilityService(new EmbeddingCompatibilityService(Options.Create(_options)), Options.Create(_options)),
            NullLogger<ModelVersionManager>.Instance);

        var result = await manager.ActivateVersionAsync(versionId, AIModelState.Production);

        Assert.Equal(AIModelState.Production, result.Status);
        _repository.Verify(r => r.DeactivateAllVersionsAsync(modelId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RollbackManager_RestoresPreviousVersion()
    {
        var modelId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var target = CreateVersionEntity(modelId, "1.0.0", AIModelState.Approved, false);
        target.Id = targetId;
        var current = CreateVersionEntity(modelId, "1.1.0", AIModelState.Production, true);

        _repository.Setup(r => r.ListVersionsAsync(modelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { current, target });
        _repository.Setup(r => r.GetModelVersionAsync(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _repository.Setup(r => r.DeactivateAllVersionsAsync(modelId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repository.Setup(r => r.UpdateModelVersionAsync(It.IsAny<Domain.Entities.AiModelVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.AddAuditEntryAsync(It.IsAny<Domain.Entities.ModelLifecycleAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var versionManager = new ModelVersionManager(
            _repository.Object,
            new ModelCompatibilityService(new EmbeddingCompatibilityService(Options.Create(_options)), Options.Create(_options)),
            NullLogger<ModelVersionManager>.Instance);

        var rollback = new ModelRollbackManager(_repository.Object, versionManager, NullLogger<ModelRollbackManager>.Instance);
        var result = await rollback.RollbackAsync(new RollbackRequest
        {
            ModelId = modelId,
            FromVersion = "1.1.0",
            ToVersion = "1.0.0",
            Reason = "Accuracy regression",
        });

        Assert.True(result.Success);
        Assert.NotNull(result.RestoredModel);
    }

    [Fact]
    public async Task DriftDetection_ReportsSeverity_WhenThresholdExceeded()
    {
        var metrics = new Mock<IRecognitionMetricsService>();
        metrics.Setup(m => m.GetSnapshotAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecognitionMetricsSnapshot
            {
                RecognitionAccuracy = 80,
                UnknownPercent = 15,
                Precision = 0.9m,
            });

        var service = new DriftDetectionService(
            metrics.Object,
            Options.Create(new ModelLifecycleOptions { DriftAccuracyThresholdPercent = 5, DriftUnknownThresholdPercent = 10 }),
            NullLogger<DriftDetectionService>.Instance);

        var report = await service.DetectAsync(new DriftDetectionRequest
        {
            ModelId = Guid.NewGuid(),
            ModelVersion = "1.0.0",
            PreviousAccuracy = 90,
        });

        Assert.NotEqual(DriftSeverity.None, report.Severity);
    }

    [Fact]
    public async Task ContinuousLearning_QueuesCandidate_WithoutRetraining()
    {
        _repository.Setup(r => r.AddRetrainingCandidateAsync(It.IsAny<RetrainingCandidate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var coordinator = new ContinuousLearningCoordinator(_repository.Object, NullLogger<ContinuousLearningCoordinator>.Instance);
        var candidate = await coordinator.QueueCandidateAsync(new QueueRetrainingCandidateRequest
        {
            TenantId = 1,
            StudentId = 42,
            CorrectionType = "ManualAssign",
        });

        Assert.Equal(42, candidate.StudentId);
        _repository.Verify(r => r.AddRetrainingCandidateAsync(It.IsAny<RetrainingCandidate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Domain.Entities.AiModelVersion CreateVersionEntity(Guid modelId, string version, AIModelState state, bool isActive) =>
        new()
        {
            Id = Guid.NewGuid(),
            ModelDefinitionId = modelId,
            Version = version,
            State = state,
            IsActive = isActive,
            EmbeddingVersion = "insightface-1.0",
            RecognitionVersion = "insightface-1.0",
            PipelineVersion = 1,
            Checksum = "abc123",
            CreatedUtc = DateTime.UtcNow,
            ModelDefinition = new Domain.Entities.AiModelDefinition
            {
                Id = modelId,
                ModelKey = "insightface",
                ModelType = AIModelType.Combined.ToString(),
                DisplayName = "InsightFace",
                CreatedUtc = DateTime.UtcNow,
            },
        };
}
