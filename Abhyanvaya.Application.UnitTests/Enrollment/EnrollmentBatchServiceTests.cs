using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Application.Enrollment.Configuration;
using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;
using Abhyanvaya.Application.Enrollment.Versioning;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Enrollment;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;
using Abhyanvaya.Infrastructure.Enrollment.PhotoProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment;

public sealed class EnrollmentBatchServiceTests
{
    private readonly Mock<IEnrollmentReferenceValidator> _referenceValidator = new();
    private readonly Mock<IStudentEnrollmentBatchRepository> _batchRepository = new();
    private readonly Mock<IStudentEnrollmentItemRepository> _itemRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPipelineVersionProvider> _pipelineVersionProvider = new();
    private readonly Mock<IPipelineManifestProvider> _pipelineManifestProvider = new();
    private readonly Mock<IEnrollmentConfigurationSnapshotCapture> _snapshotCapture = new();
    private readonly Mock<IEnrollmentEligibleStudentQuery> _eligibleStudentQuery = new();
    private readonly Mock<IEnrollmentJobQueue> _jobQueue = new();
    private readonly Mock<IStudentPhotoProviderFactory> _photoProviderFactory = new();
    private readonly Mock<IStudentPhotoProvider> _photoProvider = new();

    private EnrollmentBatchService CreateService()
    {
        _photoProvider.Setup(p => p.ProviderName).Returns("ExamBranch");
        _photoProviderFactory.Setup(f => f.GetDefaultProvider()).Returns(_photoProvider.Object);
        _photoProviderFactory.Setup(f => f.GetRegisteredProviders()).Returns(["ExamBranch"]);

        return new EnrollmentBatchService(
            _referenceValidator.Object,
            _batchRepository.Object,
            _itemRepository.Object,
            _unitOfWork.Object,
            _pipelineVersionProvider.Object,
            _pipelineManifestProvider.Object,
            _snapshotCapture.Object,
            _eligibleStudentQuery.Object,
            _jobQueue.Object,
            _photoProviderFactory.Object,
            Options.Create(new ExamBranchPhotoProviderOptions
            {
                BaseUrlTemplate = "https://example.com/{collegeCode}/{academicYear}/{studentNumber}.jpg",
            }),
            Options.Create(new EnrollmentPipelineOptions { PipelineName = "StudentEnrollment", ActiveVersion = 1 }),
            TimeProvider.System,
            NullLogger<EnrollmentBatchService>.Instance);
    }

    private static EnrollmentBatchRequest CreateRequest() =>
        new()
        {
            TenantId = 1,
            UniversityId = 10,
            CollegeId = 100,
            AcademicYear = 2026,
            RequestedByUserId = 5,
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };

    [Fact]
    public async Task CreateBatchAsync_Succeeds_WhenAllValidationPasses()
    {
        var service = CreateService();
        var request = CreateRequest();
        SetupHappyPath(request, studentCount: 2);

        var result = await service.CreateBatchAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.TotalStudents);
        Assert.Equal(BatchStatus.Created, result.Status);
        _batchRepository.Verify(r => r.CreateBatchAsync(It.IsAny<StudentEnrollmentBatch>(), It.IsAny<CancellationToken>()), Times.Once);
        _itemRepository.Verify(r => r.CreateItemsAsync(It.Is<IReadOnlyCollection<StudentEnrollmentItem>>(items => items.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        _jobQueue.Verify(q => q.SignalWork(), Times.Once);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsCollegeNotFound_WhenCollegeMissing()
    {
        var service = CreateService();
        var request = CreateRequest();

        _referenceValidator
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentReferenceValidationResult.Fail(
                EnrollmentBatchFailureCode.CollegeNotFound,
                "College missing"));

        var result = await service.CreateBatchAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(EnrollmentBatchFailureCode.CollegeNotFound, result.FailureCode);
        _batchRepository.Verify(r => r.CreateBatchAsync(It.IsAny<StudentEnrollmentBatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsActiveBatchAlreadyRunning_WhenBatchInFlight()
    {
        var service = CreateService();
        var request = CreateRequest();

        _referenceValidator
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentReferenceValidationResult.Ok("COL"));

        _batchRepository
            .Setup(r => r.HasActiveBatchAsync(request.TenantId, request.CollegeId, request.AcademicYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.CreateBatchAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(EnrollmentBatchFailureCode.ActiveBatchAlreadyRunning, result.FailureCode);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsNoEligibleStudents_WhenScopeIsEmpty()
    {
        var service = CreateService();
        var request = CreateRequest();
        SetupThroughPipeline(request);

        _eligibleStudentQuery
            .Setup(q => q.GetEligibleStudentsAsync(It.IsAny<EnrollmentStudentDiscoveryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EnrollmentEligibleStudent>());

        var result = await service.CreateBatchAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(EnrollmentBatchFailureCode.NoEligibleStudents, result.FailureCode);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsSnapshotFailure_WhenCaptureFails()
    {
        var service = CreateService();
        var request = CreateRequest();
        SetupThroughPipeline(request);

        _eligibleStudentQuery
            .Setup(q => q.GetEligibleStudentsAsync(It.IsAny<EnrollmentStudentDiscoveryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new EnrollmentEligibleStudent { StudentId = 1, StudentNumber = "S1" }]);

        _snapshotCapture
            .Setup(s => s.CaptureAsync(request, 1, It.IsAny<PipelineManifest>(), "ExamBranch", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConfigurationSnapshotCaptureResult.Fail("Snapshot incomplete"));

        var result = await service.CreateBatchAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(EnrollmentBatchFailureCode.ConfigurationSnapshotFailed, result.FailureCode);
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateBatchAsync_ReturnsQueueFailure_WhenSignalWorkThrows()
    {
        var service = CreateService();
        var request = CreateRequest();
        SetupHappyPath(request, studentCount: 1);

        _jobQueue.Setup(q => q.SignalWork()).Throws(new InvalidOperationException("queue down"));

        var result = await service.CreateBatchAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(EnrollmentBatchFailureCode.QueueFailed, result.FailureCode);
        _unitOfWork.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBatchAsync_RollsBack_WhenTransactionFails()
    {
        var service = CreateService();
        var request = CreateRequest();
        SetupHappyPath(request, studentCount: 1, skipTransaction: true);

        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db failure"));

        var result = await service.CreateBatchAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal(EnrollmentBatchFailureCode.PersistenceFailed, result.FailureCode);
        _jobQueue.Verify(q => q.SignalWork(), Times.Never);
    }

    private void SetupHappyPath(EnrollmentBatchRequest request, int studentCount, bool skipTransaction = false)
    {
        SetupThroughPipeline(request);

        var students = Enumerable.Range(1, studentCount)
            .Select(i => new EnrollmentEligibleStudent { StudentId = i, StudentNumber = $"S{i}" })
            .ToList();

        _eligibleStudentQuery
            .Setup(q => q.GetEligibleStudentsAsync(It.IsAny<EnrollmentStudentDiscoveryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(students);

        _snapshotCapture
            .Setup(s => s.CaptureAsync(request, 1, It.IsAny<PipelineManifest>(), "ExamBranch", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConfigurationSnapshotCaptureResult.Ok(
                new ConfigurationSnapshot
                {
                    SchemaVersion = 1,
                    CapturedUtc = DateTime.UtcNow,
                    SnapshotHash = "sha256:test",
                    EmbeddingProvider = "InsightFace",
                    EmbeddingModel = "w600k_r50",
                    EmbeddingVersion = "insightface-r50-v1.0",
                    EngineProvider = "InsightFace",
                    NormalizationMethod = "L2",
                    AiModelVersion = "1.0",
                    Thresholds = new ThresholdSnapshot
                    {
                        RecognitionMatchDistanceThreshold = 0.45f,
                        RecognitionLowConfidenceDistanceThreshold = 0.55f,
                        DetectionThreshold = 0.5f,
                        NmsThreshold = 0.4f,
                    },
                    PhotoProvider = "ExamBranch",
                    PhotoProviderSettings = new Dictionary<string, string>(),
                    ValidationRules = new ValidationRulesSnapshot
                    {
                        RequireExactlyOneFace = true,
                        MinimumSourceWidth = 1,
                        MinimumSourceHeight = 1,
                        MinimumFaceWidth = 32,
                        MinimumFaceHeight = 32,
                        BlurMethod = "VarianceOfLaplacian",
                        BlurThreshold = 100,
                        MaximumAbsoluteYawDegrees = 25,
                        MaximumAbsolutePitchDegrees = 25,
                        MaximumAbsoluteRollDegrees = 25,
                        CompositeQualityIsAdvisory = true,
                        CompositeQualityWeights = new Dictionary<string, double>(),
                    },
                    StorageProvider = "local",
                    StorageProviderSettings = new Dictionary<string, string>(),
                    PipelineVersion = 1,
                    PipelineManifest = new PipelineManifestReference
                    {
                        PipelineName = "StudentEnrollment",
                        PipelineVersion = 1,
                        ManifestSchemaVersion = 1,
                        ManifestHash = "sha256:manifest",
                    },
                    RetryPolicy = new RetryPolicySnapshot
                    {
                        PolicySetVersion = "v1",
                        StagePolicyNames = new Dictionary<string, string>(),
                        MaxAutomaticRetries = 3,
                        MaximumAutomaticAttempts = 4,
                        RetryWindow = TimeSpan.FromHours(24),
                        BackoffStrategy = "ExponentialFullJitter",
                        BaseDelay = TimeSpan.FromSeconds(30),
                        MaxDelay = TimeSpan.FromMinutes(30),
                        MaximumConsecutiveImmediateRetries = 1,
                        BatchBudget = new RetryBudgetSnapshot
                        {
                            CapacityTokens = 25,
                            RefillTokens = 1,
                            RefillInterval = TimeSpan.FromHours(1),
                            LifetimeSpendCeilingTokens = 50,
                        },
                        StageBudgetCosts = new Dictionary<string, int>(),
                        AutomaticRetrySafetyFloor = [],
                        ScheduledRetryHonorsRetryAfter = true,
                        LowLevelPhotoImport = new StageInvocationRetrySnapshot
                        {
                            MaxRetriesWithinAttempt = 3,
                            BackoffSeconds = [2, 4, 8],
                            RetryableConditions = [],
                        },
                    },
                    FeatureFlags = new Dictionary<string, bool>(),
                },
                "{\"schemaVersion\":1}"));

        if (!skipTransaction)
        {
            _unitOfWork
                .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));
        }
    }

    private void SetupThroughPipeline(EnrollmentBatchRequest request)
    {
        _referenceValidator
            .Setup(v => v.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentReferenceValidationResult.Ok("COL"));

        _batchRepository
            .Setup(r => r.HasActiveBatchAsync(request.TenantId, request.CollegeId, request.AcademicYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _pipelineVersionProvider
            .Setup(p => p.GetActiveVersionForNewBatch(request))
            .Returns(new PipelineVersion(1));

        _pipelineVersionProvider.Setup(p => p.VersionExists(new PipelineVersion(1))).Returns(true);

        _pipelineManifestProvider
            .Setup(p => p.ManifestExists("StudentEnrollment", 1))
            .Returns(true);

        _pipelineManifestProvider
            .Setup(p => p.GetManifest("StudentEnrollment", 1))
            .Returns(EnrollmentPipelineDefaults.CreateV1Manifest());
    }

    [Fact]
    public async Task CancelBatchAsync_CancelsRunningBatch_WithoutUpdatingRowVersionManually()
    {
        var batchId = Guid.NewGuid();
        var batch = new StudentEnrollmentBatch
        {
            Id = batchId,
            TenantId = 1,
            Status = BatchStatus.Running,
            PhotoProviderName = "ExamBranch",
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

        _batchRepository
            .Setup(r => r.GetBatchAsync(batchId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(batch);

        _itemRepository
            .Setup(r => r.CancelNonTerminalItemsAsync(batchId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(298);

        _itemRepository
            .Setup(r => r.CountByStatusAsync(batchId, EnrollmentStatus.Completed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _itemRepository
            .Setup(r => r.CountByStatusAsync(batchId, EnrollmentStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _itemRepository
            .Setup(r => r.CountByStatusAsync(batchId, EnrollmentStatus.Cancelled, It.IsAny<CancellationToken>()))
            .ReturnsAsync(298);

        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));

        var service = CreateService();
        var result = await service.CancelBatchAsync(batchId, 1, 42);

        Assert.True(result.Applied);
        Assert.Equal(BatchStatus.Cancelled, result.Status);
        Assert.Equal(BatchStatus.Cancelled, batch.Status);
        Assert.NotNull(batch.CancellationRequestedUtc);
        _itemRepository.Verify(
            r => r.CancelNonTerminalItemsAsync(batchId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _itemRepository.Verify(
            r => r.UpdateItemAsync(It.IsAny<StudentEnrollmentItem>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelBatchAsync_ReturnsNoOp_WhenBatchAlreadyCancelled()
    {
        var batchId = Guid.NewGuid();
        _batchRepository
            .Setup(r => r.GetBatchAsync(batchId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentEnrollmentBatch
            {
                Id = batchId,
                TenantId = 1,
                Status = BatchStatus.Cancelled,
                PhotoProviderName = "ExamBranch",
                RowVersion = Guid.NewGuid().ToByteArray(),
            });

        var service = CreateService();
        var result = await service.CancelBatchAsync(batchId, 1, 42);

        Assert.False(result.Applied);
        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
