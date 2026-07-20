using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;
using Abhyanvaya.Infrastructure.Enrollment.Orchestration;
using Abhyanvaya.Infrastructure.Enrollment.Orchestration.Stages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Orchestration;

public sealed class EnrollmentOrchestratorTests
{
    private readonly Mock<IEnrollmentValidationService> _validationService = new();
    private readonly Mock<IEnrollmentStorageService> _storageService = new();
    private readonly Mock<IEnrollmentStudentPhotoPublisher> _studentPhotoPublisher = new();
    private readonly Mock<IEnrollmentEmbeddingService> _embeddingService = new();
    private readonly Mock<IEnrollmentResultWriter> _resultWriter = new();
    private readonly Mock<IEnrollmentProgressReporter> _progressReporter = new();
    private readonly Mock<IStudentPhotoProviderFactory> _photoProviderFactory = new();
    private readonly Mock<IStudentPhotoProvider> _photoProvider = new();
    private readonly Mock<IPipelineManifestProvider> _manifestProvider = new();
    private readonly Mock<IEnrollmentPipelineMetrics> _metrics = new();
    private readonly IEnrollmentRetryPolicy _retryPolicy = new DefaultEnrollmentRetryPolicy();

    public EnrollmentOrchestratorTests()
    {
        _manifestProvider.Setup(m => m.GetManifest(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(EnrollmentPipelineDefaults.CreateV1Manifest());

        _photoProvider.Setup(p => p.ProviderName).Returns("ExamBranch");
        _photoProviderFactory.Setup(f => f.GetProvider(It.IsAny<string>())).Returns(_photoProvider.Object);

        _progressReporter.Setup(p => p.MarkItemStartedAsync(It.IsAny<EnrollmentProgressOperationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentTransitionResult.AppliedOk());
        _progressReporter.Setup(p => p.MarkStageCompletedAsync(It.IsAny<EnrollmentStageProgressRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentTransitionResult.AppliedOk());
        _progressReporter.Setup(p => p.MarkStageFailedAsync(It.IsAny<EnrollmentStageProgressRequest>(), It.IsAny<EnrollmentStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentTransitionResult.AppliedOk());
        _progressReporter.Setup(p => p.UpdateProgressAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentProgressDetail?)null);
        _progressReporter.Setup(p => p.FinalizeBatchIfCompleteAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _studentPhotoPublisher.Setup(p => p.PublishAsync(It.IsAny<EnrollmentStudentPhotoPublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentStudentPhotoPublishResult.Succeeded("students/1/42", DateTime.UtcNow));
    }

    [Fact]
    public async Task ProcessItemAsync_Succeeds_WhenAllStagesPass()
    {
        SetupSuccessfulPipeline();

        var result = await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: false));

        Assert.True(result.Success);
        Assert.Equal(EnrollmentPipelineState.Completed, result.Status);
        Assert.Equal(EnrollmentStatus.Completed, result.ItemStatus);
        Assert.NotNull(result.StageResults);
        Assert.Contains(result.StageResults!, stage => stage.StageName == "Validation" && stage.Success);
        Assert.Contains(result.StageResults!, stage => stage.StageName == "Persistence" && stage.Success);
        _resultWriter.Verify(w => w.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessItemAsync_StopsOnValidationFailure()
    {
        SetupDownload();
        _validationService.Setup(v => v.ValidateAsync(It.IsAny<EnrollmentValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentValidationResult
            {
                ValidationPassed = false,
                Report = CreateReport(),
                FailureCategory = FailureCategory.NoFaceDetected,
                FailureReason = "No face detected.",
                Duration = TimeSpan.FromMilliseconds(10),
            });

        var result = await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: true));

        Assert.False(result.Success);
        Assert.Equal(FailureCategory.NoFaceDetected, result.FailureCategory);
        _storageService.Verify(s => s.StoreAsync(It.IsAny<EnrollmentStorageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessItemAsync_StopsOnStorageFailure()
    {
        SetupDownload();
        SetupValidation();
        _storageService.Setup(s => s.StoreAsync(It.IsAny<EnrollmentStorageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentStorageResult
            {
                Success = false,
                Duration = TimeSpan.FromMilliseconds(5),
                FailureReason = "Upload failed.",
            });

        var result = await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: true));

        Assert.False(result.Success);
        Assert.Equal("storage.failure", result.FailureCode);
        _embeddingService.Verify(e => e.GenerateAsync(It.IsAny<EnrollmentEmbeddingRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessItemAsync_StopsOnEmbeddingFailure()
    {
        SetupDownload();
        SetupValidation();
        SetupStorage();
        _embeddingService.Setup(e => e.GenerateAsync(It.IsAny<EnrollmentEmbeddingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentEmbeddingResult.Failed(
                EnrollmentEmbeddingFailureCodes.EmbeddingFailure,
                "Engine failure."));

        var result = await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: true));

        Assert.False(result.Success);
        _resultWriter.Verify(w => w.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessItemAsync_StopsOnPersistenceFailure()
    {
        SetupDownload();
        SetupValidation();
        SetupStorage();
        SetupEmbedding();
        _resultWriter.Setup(w => w.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentPersistenceResult.Failed(
                CreateArtifact(),
                TimeSpan.FromMilliseconds(5),
                EnrollmentPersistenceFailureCodes.DatabaseFailure,
                "Database failure."));

        var result = await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: true));

        Assert.False(result.Success);
        Assert.Equal(EnrollmentPersistenceFailureCodes.DatabaseFailure, result.FailureCode);
    }

    [Fact]
    public async Task ProcessItemAsync_PropagatesCancellation()
    {
        SetupDownload();
        _validationService.Setup(v => v.ValidateAsync(It.IsAny<EnrollmentValidationRequest>(), It.IsAny<CancellationToken>()))
            .Returns<EnrollmentValidationRequest, CancellationToken>((_, ct) => Task.FromCanceled<EnrollmentValidationResult>(ct));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: true), cts.Token));
    }

    [Fact]
    public async Task ProcessItemAsync_ReportsProgressOnSuccess()
    {
        SetupSuccessfulPipeline();

        await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: false));

        _progressReporter.Verify(p => p.UpdateProgressAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _progressReporter.Verify(p => p.FinalizeBatchIfCompleteAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessItemAsync_RetriesTransientStorageFailure()
    {
        SetupDownload();
        SetupValidation();

        var attempts = 0;
        _storageService.Setup(s => s.StoreAsync(It.IsAny<EnrollmentStorageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempts++;
                if (attempts == 1)
                {
                    return new EnrollmentStorageResult
                    {
                        Success = false,
                        Duration = TimeSpan.FromMilliseconds(5),
                        FailureReason = "Transient upload failure.",
                    };
                }

                return CreateStorageResult();
            });

        SetupEmbedding();
        SetupPersistence();

        var result = await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: true));

        Assert.True(result.Success);
        Assert.True(result.Statistics!.RetryCount > 0);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ProcessItemAsync_IncludesTelemetryInResult()
    {
        SetupSuccessfulPipeline();

        var result = await CreateOrchestrator().ProcessItemAsync(CreateRequest(includeDownload: false));

        Assert.NotNull(result.Statistics);
        Assert.True(result.Statistics!.TotalDuration >= TimeSpan.Zero);
        Assert.NotNull(result.Statistics.StageDurations);
        Assert.True(result.Statistics.StageDurations!.ContainsKey("Validation"));
        Assert.Equal(TestCorrelationId, result.CorrelationId);
    }

    [Fact]
    public void Registry_OrdersPersistenceBetweenEmbeddingAndFinalize()
    {
        var registry = CreateRegistry();
        var stages = registry.GetOrderedStages(1);

        var names = stages.Select(stage => stage.Name).ToList();
        var embeddingIndex = names.IndexOf("Embedding");
        var persistenceIndex = names.IndexOf("Persistence");
        var progressIndex = names.IndexOf("Progress");

        Assert.True(embeddingIndex >= 0);
        Assert.True(persistenceIndex > embeddingIndex);
        Assert.True(progressIndex > persistenceIndex);
    }

    private static readonly Guid TestBatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TestCorrelationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestTraceId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private void SetupSuccessfulPipeline()
    {
        SetupValidation();
        SetupStorage();
        SetupEmbedding();
        SetupPersistence();
    }

    private void SetupDownload()
    {
        _photoProvider.Setup(p => p.FetchPhotoAsync(It.IsAny<StudentPhotoFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(StudentPhotoFetchResult.Successful([1, 2, 3], "image/jpeg", "https://example.test/photo.jpg"));
    }

    private void SetupValidation()
    {
        _validationService.Setup(v => v.ValidateAsync(It.IsAny<EnrollmentValidationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentValidationResult
            {
                ValidationPassed = true,
                Report = CreateReport(),
                Duration = TimeSpan.FromMilliseconds(10),
                Artifact = CreateValidationArtifact(),
            });
    }

    private void SetupStorage()
    {
        _storageService.Setup(s => s.StoreAsync(It.IsAny<EnrollmentStorageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStorageResult());
    }

    private void SetupEmbedding()
    {
        _embeddingService.Setup(e => e.GenerateAsync(It.IsAny<EnrollmentEmbeddingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentEmbeddingResult.Succeeded(
                CreateArtifact(),
                new EmbeddingMetadata
                {
                    Model = "w600k_r50.onnx",
                    ModelVersion = "1.0",
                    EmbeddingDimension = 512,
                    Normalization = "L2",
                    ExecutionDevice = "CPU",
                    ExecutionTime = TimeSpan.FromMilliseconds(5),
                },
                new EmbeddingValidationStatistics(
                    Dimension: 512,
                    Magnitude: 1f,
                    MinValue: -1f,
                    MaxValue: 1f,
                    Mean: 0f,
                    IsNormalized: true),
                [],
                new EnrollmentEmbeddingTelemetry
                {
                    ResolveDuration = TimeSpan.Zero,
                    InferenceDuration = TimeSpan.FromMilliseconds(5),
                    NormalizationDuration = TimeSpan.Zero,
                    ValidationDuration = TimeSpan.Zero,
                    TotalDuration = TimeSpan.FromMilliseconds(5),
                }));
    }

    private void SetupPersistence()
    {
        _resultWriter.Setup(w => w.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentPersistenceResult.Succeeded(
                42,
                TestBatchId,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                EnrollmentStatus.Completed,
                EnrollmentPersistenceState.ReadyForRecognition,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(5),
                CreateArtifact(),
                null,
                new EnrollmentPersistenceStatistics
                {
                    WriteDuration = TimeSpan.FromMilliseconds(5),
                    DatabaseDuration = TimeSpan.FromMilliseconds(2),
                    TransactionDuration = TimeSpan.FromMilliseconds(3),
                    RowsInserted = 3,
                    RowsUpdated = 2,
                }));
    }

    private EnrollmentOrchestrator CreateOrchestrator() =>
        new(new EnrollmentPipelineExecutor(
            CreateRegistry(),
            _retryPolicy,
            _metrics.Object,
            NullLogger<EnrollmentPipelineExecutor>.Instance),
            NullLogger<EnrollmentOrchestrator>.Instance);

    private EnrollmentPipelineRegistry CreateRegistry() =>
        new(_manifestProvider.Object,
        [
            new DownloadEnrollmentPipelineStage(_photoProviderFactory.Object, _progressReporter.Object),
            new ValidationEnrollmentPipelineStage(_validationService.Object, _progressReporter.Object),
            new StorageEnrollmentPipelineStage(_storageService.Object, _studentPhotoPublisher.Object),
            new EmbeddingEnrollmentPipelineStage(_embeddingService.Object),
            new PersistenceEnrollmentPipelineStage(_resultWriter.Object),
            new ProgressEnrollmentPipelineStage(_progressReporter.Object),
        ]);

    private static EnrollmentPipelineRequest CreateRequest(bool includeDownload)
    {
        var context = new EnrollmentItemContext
        {
            BatchId = TestBatchId,
            ItemId = TestItemId,
            TenantId = 1,
            StudentId = 42,
            StudentNumber = "STU001",
            CollegeCode = "COL",
            CollegeId = 10,
            AcademicYear = 2026,
            PhotoProviderName = "ExamBranch",
            ExecutionTraceId = TestTraceId,
            CorrelationId = TestCorrelationId,
            PipelineVersion = 1,
        };

        return new EnrollmentPipelineRequest
        {
            Context = context,
            ItemStatus = includeDownload ? EnrollmentStatus.Pending : EnrollmentStatus.Downloaded,
            PhotoBytes = includeDownload ? null : [1, 2, 3, 4],
            ContentType = "image/jpeg",
            ByteSize = 4,
        };
    }

    private static ValidationReport CreateReport() =>
        new()
        {
            OverallResult = ValidationOverallResult.Passed,
            FaceCount = 1,
            CompositeScore = 0.9f,
            SourceWidth = 640,
            SourceHeight = 480,
            RuleResults = [],
            ValidationFailures = [],
            Warnings = [],
            EmbeddingEligible = true,
            SeveritySummary = new ValidationSeveritySummary
            {
                PassCount = 1,
                FailCount = 0,
                WarningCount = 0,
                InformationCount = 0,
                SkippedCount = 0,
                NotApplicableCount = 0,
            },
        };

    private static EnrollmentValidationArtifact CreateValidationArtifact() =>
        new()
        {
            Report = CreateReport(),
            AlignedFaceImage = [9, 8, 7],
            TimestampUtc = DateTimeOffset.UtcNow,
            CorrelationId = TestCorrelationId,
        };

    private static EnrollmentStorageResult CreateStorageResult() =>
        new()
        {
            Success = true,
            Duration = TimeSpan.FromMilliseconds(5),
            Manifest = new EnrollmentStorageManifest
            {
                ManifestId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                StorageGroupId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                ManifestVersion = 1,
                SchemaVersion = 1,
                PipelineVersion = 1,
                ValidationVersion = 1,
                StorageVersion = 1,
                ArtifactVersion = 1,
                CorrelationId = TestCorrelationId,
                CreatedUtc = DateTimeOffset.UtcNow,
                Entries = [],
            },
        };

    private static EnrollmentEmbeddingArtifact CreateArtifact() =>
        new()
        {
            StudentId = 42,
            BatchId = TestBatchId,
            EmbeddingVector = Enumerable.Repeat(0.01f, 512).ToArray(),
            EmbeddingDimension = 512,
            EmbeddingModel = "w600k_r50.onnx",
            EmbeddingModelVersion = "1.0",
            PipelineVersion = 1,
            ValidationVersion = 1,
            StorageVersion = 1,
            ArtifactVersion = 1,
            ManifestVersion = 1,
            QualityScore = 0.9f,
            CorrelationId = TestCorrelationId,
            EmbeddingDuration = TimeSpan.FromMilliseconds(5),
            CreatedUtc = DateTimeOffset.UtcNow,
        };
}
