using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Enrollment.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Persistence;

public sealed class EnrollmentResultWriterTests
{
    private readonly Mock<IEnrollmentPersistenceRepository> _persistenceRepository = new();
    private readonly Mock<IEnrollmentPersistencePolicy> _policy = new();
    private readonly Mock<IEnrollmentDuplicateDetector> _duplicateDetector = new();
    private readonly Mock<IEnrollmentPersistenceMetrics> _metrics = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public EnrollmentResultWriterTests()
    {
        _currentUser.Setup(c => c.UserId).Returns(99);
        _duplicateDetector.Setup(d => d.DetectAsync(It.IsAny<EnrollmentDuplicateDetectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDuplicateDetectionResult { IsDuplicate = false });
        _policy.Setup(p => p.Evaluate(It.IsAny<EnrollmentPersistencePolicyContext>()))
            .Returns(new EnrollmentPersistencePolicyDecision { AllowPersist = true, KeepHistoricalVersions = true });

        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));

        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _persistenceRepository.Setup(r => r.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentPersistenceWriteOutcome
            {
                EmbeddingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                RowsInserted = 3,
                RowsUpdated = 2,
            });
    }

    [Fact]
    public async Task PersistEmbeddingAsync_Succeeds_WhenItemIsInEmbeddingStatus()
    {
        SetupContext(CreateItem(EnrollmentStatus.Embedding));

        var result = await CreateWriter().PersistEmbeddingAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), result.EmbeddingId);
        Assert.Equal(EnrollmentStatus.Completed, result.Status);
        Assert.Equal(EnrollmentPersistenceState.ReadyForRecognition, result.PersistenceState);
        _persistenceRepository.Verify(r => r.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceWriteRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PersistEmbeddingAsync_ReturnsDuplicate_WhenAlreadyPersisted()
    {
        var existingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var item = CreateItem(EnrollmentStatus.Completed);
        item.StudentFaceEmbeddingId = existingId;
        item.EmbeddingVersion = "insightface-w600k-r50-vinsightface-1.0";
        SetupContext(item);

        _duplicateDetector.Setup(d => d.DetectAsync(It.IsAny<EnrollmentDuplicateDetectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDuplicateDetectionResult
            {
                IsDuplicate = true,
                ExistingEmbeddingId = existingId,
            });

        var result = await CreateWriter().PersistEmbeddingAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.True(result.IsDuplicate);
        Assert.Equal(existingId, result.EmbeddingId);
        _persistenceRepository.Verify(r => r.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PersistEmbeddingAsync_ReturnsMissingEnrollment_WhenContextNotFound()
    {
        _persistenceRepository.Setup(r => r.LoadContextAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentPersistenceContext?)null);

        var result = await CreateWriter().PersistEmbeddingAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentPersistenceFailureCodes.MissingEnrollment, result.FailureCode);
    }

    [Fact]
    public async Task PersistEmbeddingAsync_ReturnsValidationMismatch_WhenStatusIsNotEmbedding()
    {
        SetupContext(CreateItem(EnrollmentStatus.Validating));

        var result = await CreateWriter().PersistEmbeddingAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentPersistenceFailureCodes.ValidationMismatch, result.FailureCode);
    }

    [Fact]
    public async Task PersistEmbeddingAsync_ReturnsConcurrencyConflict_OnDbUpdateConcurrencyException()
    {
        SetupContext(CreateItem(EnrollmentStatus.Embedding));

        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("RowVersion conflict."));

        var result = await CreateWriter().PersistEmbeddingAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentPersistenceFailureCodes.ConcurrencyConflict, result.FailureCode);
    }

    [Fact]
    public async Task PersistEmbeddingAsync_ReturnsDatabaseFailure_WhenRepositoryThrows()
    {
        SetupContext(CreateItem(EnrollmentStatus.Embedding));

        _persistenceRepository.Setup(r => r.PersistEmbeddingAsync(It.IsAny<EnrollmentPersistenceWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Repository failure."));

        var result = await CreateWriter().PersistEmbeddingAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentPersistenceFailureCodes.DatabaseFailure, result.FailureCode);
    }

    [Fact]
    public async Task PersistEmbeddingAsync_PropagatesCancellation()
    {
        SetupContext(CreateItem(EnrollmentStatus.Embedding));

        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((_, ct) => Task.FromCanceled(ct));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateWriter().PersistEmbeddingAsync(CreateRequest(), cts.Token));
    }

    [Fact]
    public async Task PersistEmbeddingAsync_HandlesLargeEmbeddingVector()
    {
        SetupContext(CreateItem(EnrollmentStatus.Embedding));

        var largeVector = Enumerable.Repeat(1f / MathF.Sqrt(512), 512).ToArray();
        var request = CreateRequest() with
        {
            Artifact = CreateArtifact() with { EmbeddingVector = largeVector, EmbeddingDimension = 512 },
        };

        var result = await CreateWriter().PersistEmbeddingAsync(request);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task PersistEmbeddingAsync_IsIdempotent_OnRetry()
    {
        SetupContext(CreateItem(EnrollmentStatus.Embedding));

        var existingId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        _duplicateDetector.Setup(d => d.DetectAsync(It.IsAny<EnrollmentDuplicateDetectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentDuplicateDetectionResult
            {
                IsDuplicate = true,
                ExistingEmbeddingId = existingId,
            });

        var result = await CreateWriter().PersistEmbeddingAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.True(result.IsDuplicate);
        Assert.Equal(existingId, result.EmbeddingId);
    }

    [Fact]
    public void Policy_RejectsTerminalStatuses()
    {
        var policy = new DefaultEnrollmentPersistencePolicy();
        var decision = policy.Evaluate(new EnrollmentPersistencePolicyContext
        {
            ItemId = Guid.NewGuid(),
            StudentId = 1,
            BatchId = Guid.NewGuid(),
            CurrentStatus = EnrollmentStatus.Failed,
            RequestedEmbeddingVersion = "v1",
            PipelineVersion = 1,
        });

        Assert.False(decision.AllowPersist);
    }

    private EnrollmentResultWriter CreateWriter() =>
        new(
            _persistenceRepository.Object,
            _policy.Object,
            _duplicateDetector.Object,
            _metrics.Object,
            _unitOfWork.Object,
            _currentUser.Object,
            TimeProvider.System,
            NullLogger<EnrollmentResultWriter>.Instance);

    private void SetupContext(StudentEnrollmentItem item)
    {
        _persistenceRepository.Setup(r => r.LoadContextAsync(item.BatchId, item.StudentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentPersistenceContext
            {
                Item = item,
                Batch = new StudentEnrollmentBatch
                {
                    Id = item.BatchId,
                    TenantId = 1,
                    UniversityId = 1,
                    CollegeId = 10,
                    AcademicYear = 2026,
                    Status = BatchStatus.Running,
                    TotalStudents = 1,
                    EmbeddingCount = 1,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = 1,
                    PipelineVersion = 1,
                    ConfigurationSnapshotJson = "{}",
                    CorrelationId = Guid.NewGuid(),
                    PhotoProviderName = "ExamBranch",
                },
                Student = new Student
                {
                    Id = item.StudentId,
                    TenantId = 1,
                    StudentNumber = "S42",
                    Name = "Test",
                    CourseId = 1,
                    GroupId = 1,
                    GenderId = 1,
                    MediumId = 1,
                    FirstLanguageId = 1,
                    LanguageId = 1,
                    SemesterId = 1,
                },
            });
    }

    private static EnrollmentPersistenceRequest CreateRequest() =>
        new() { Artifact = CreateArtifact() };

    private static EnrollmentEmbeddingArtifact CreateArtifact()
    {
        var vector = Enumerable.Repeat(1f / MathF.Sqrt(512), 512).ToArray();
        return new EnrollmentEmbeddingArtifact
        {
            StudentId = 42,
            BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            EmbeddingVector = vector,
            EmbeddingDimension = 512,
            EmbeddingModel = "w600k_r50.onnx",
            EmbeddingModelVersion = "insightface-w600k-r50-vinsightface-1.0",
            PipelineVersion = 1,
            ValidationVersion = 1,
            StorageVersion = 1,
            ArtifactVersion = 1,
            ManifestVersion = 1,
            QualityScore = 0.95f,
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            EmbeddingDuration = TimeSpan.FromMilliseconds(20),
            CreatedUtc = DateTimeOffset.UtcNow,
        };
    }

    private static StudentEnrollmentItem CreateItem(EnrollmentStatus status) =>
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            TenantId = 1,
            BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            StudentId = 42,
            Status = status,
            SourceUrl = "https://example.com/photo.jpg",
            PhotoKey = "students/1/42/photo.webp",
            RowVersion = [1, 2, 3, 4],
        };
}
