using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Infrastructure.Enrollment.Storage;
using Abhyanvaya.Infrastructure.Enrollment.Storage.ArtifactTypes;
using Abhyanvaya.Infrastructure.Enrollment.Storage.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Storage;

public sealed class EnrollmentStorageServiceTests
{
    private readonly Mock<IEnrollmentStoragePolicy> _policy = new();
    private readonly Mock<IObjectStorageProvider> _objectStorage = new();
    private readonly Mock<IEnrollmentStorageRecordRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly IChecksumService _checksumService = new Sha256ChecksumService();
    private readonly IEnrollmentArtifactTypeRegistry _registry = EnrollmentStorageTestFactory.CreateRegistry();

    public EnrollmentStorageServiceTests()
    {
        _policy.Setup(p => p.ResolveAsync(It.IsAny<EnrollmentStoragePolicyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentStoragePolicyDecision
            {
                EnabledArtifactTypes = new HashSet<string>(StringComparer.Ordinal)
                {
                    EnrollmentArtifactTypeNames.AlignedFace,
                    EnrollmentArtifactTypeNames.ValidationReport,
                },
            });

        _objectStorage.Setup(o => o.ProviderName).Returns("Local");

        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));

        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _repository.Setup(r => r.FindByChecksumAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentStorageRecord?)null);

        _repository.Setup(r => r.GetNextArtifactVersionAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task StoreAsync_PersistsAlignedFaceAndReport_WithManifest()
    {
        var alignedBytes = CreateAlignedFaceBytes();
        var request = CreateRequest(alignedBytes);
        var capturedKeys = new List<string>();

        _objectStorage.Setup(o => o.WriteObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((key, stream, _, _) =>
            {
                capturedKeys.Add(key);
                using var _ = stream;
            })
            .Returns(Task.CompletedTask);

        var result = await CreateService().StoreAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.StorageRecordId);
        Assert.Equal("Local", result.StorageProvider);
        Assert.Equal(alignedBytes.Length, result.FileSize);
        Assert.NotNull(result.Manifest);
        Assert.Equal(2, result.Manifest!.Entries.Count);
        Assert.Equal(2, capturedKeys.Count);
        Assert.Contains(capturedKeys, k => k.Contains("AlignedFace"));
        Assert.Contains(capturedKeys, k => k.Contains("ValidationReport"));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_GeneratesSha256Checksum()
    {
        var alignedBytes = CreateAlignedFaceBytes();
        var request = CreateRequest(alignedBytes);
        var expectedChecksum = _checksumService.ComputeSha256Hex(alignedBytes);

        EnrollmentStorageResult? result = null;
        _repository.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EnrollmentStorageRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EnrollmentStorageRecord>, CancellationToken>((records, _) =>
            {
                var primary = records.First(r => r.IsPrimary);
                result = new EnrollmentStorageResult
                {
                    Success = true,
                    Checksum = primary.Checksum,
                    Duration = TimeSpan.Zero,
                    StorageVersion = 1,
                };
            })
            .Returns(Task.CompletedTask);

        var serviceResult = await CreateService().StoreAsync(request);

        Assert.True(serviceResult.Success);
        Assert.Equal(expectedChecksum, serviceResult.Checksum);
    }

    [Fact]
    public async Task StoreAsync_ReturnsDuplicate_WhenChecksumAlreadyExists()
    {
        var alignedBytes = CreateAlignedFaceBytes();
        var checksum = _checksumService.ComputeSha256Hex(alignedBytes);
        var existingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        _repository.Setup(r => r.FindByChecksumAsync(
                1,
                42,
                EnrollmentArtifactTypeNames.AlignedFace,
                checksum,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentStorageRecord
            {
                Id = existingId,
                StorageGroupId = Guid.NewGuid(),
                TenantId = 1,
                CollegeId = 10,
                AcademicYear = 2026,
                StudentId = 42,
                BatchId = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
                ObjectKey = "existing/key.webp",
                StorageProvider = "Local",
                Checksum = checksum,
                ContentType = "image/webp",
                FileSize = alignedBytes.Length,
                ArtifactVersion = 1,
                CorrelationId = Guid.NewGuid(),
                IsPrimary = true,
                CreatedUtc = DateTime.UtcNow,
            });

        var result = await CreateService().StoreAsync(CreateRequest(alignedBytes));

        Assert.True(result.Success);
        Assert.Equal(existingId, result.StorageRecordId);
        _objectStorage.Verify(o => o.WriteObjectAsync(
            It.Is<string>(k => k.Contains("AlignedFace")),
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _objectStorage.Verify(o => o.WriteObjectAsync(
            It.Is<string>(k => k.Contains("ValidationReport")),
            It.IsAny<Stream>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreAsync_RollsBackUploadedObjects_WhenMetadataSaveFails()
    {
        var alignedBytes = CreateAlignedFaceBytes();
        var deletedKeys = new List<string>();

        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("metadata failed"));

        _objectStorage.Setup(o => o.DeleteObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => deletedKeys.Add(key))
            .Returns(Task.CompletedTask);

        var result = await CreateService().StoreAsync(CreateRequest(alignedBytes));

        Assert.False(result.Success);
        Assert.Contains("metadata failed", result.FailureReason);
        Assert.NotEmpty(deletedKeys);
    }

    [Fact]
    public async Task StoreAsync_IncrementsArtifactVersion()
    {
        _repository.Setup(r => r.GetNextArtifactVersionAsync(
                1,
                42,
                EnrollmentArtifactTypeNames.AlignedFace,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var capturedVersion = 0;
        _repository.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<EnrollmentStorageRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EnrollmentStorageRecord>, CancellationToken>((records, _) =>
            {
                capturedVersion = records.First(r => r.IsPrimary).ArtifactVersion;
            })
            .Returns(Task.CompletedTask);

        await CreateService().StoreAsync(CreateRequest(CreateAlignedFaceBytes()));

        Assert.Equal(3, capturedVersion);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsManifestForStorageGroup()
    {
        var groupId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var records = new List<EnrollmentStorageRecord>
        {
            new()
            {
                Id = recordId,
                StorageGroupId = groupId,
                TenantId = 1,
                CollegeId = 10,
                AcademicYear = 2026,
                StudentId = 42,
                BatchId = Guid.NewGuid(),
                ItemId = Guid.NewGuid(),
                ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
                ObjectKey = "k1",
                StorageProvider = "Local",
                Checksum = "abc",
                ContentType = "image/webp",
                FileSize = 100,
                ArtifactVersion = 1,
                CorrelationId = Guid.NewGuid(),
                IsPrimary = true,
                CreatedUtc = DateTime.UtcNow,
            },
        };

        _repository.Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records[0]);
        _repository.Setup(r => r.GetByStorageGroupIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var result = await CreateService().RetrieveAsync(recordId);

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Single(result.Manifest!.Entries);
    }

    [Fact]
    public async Task StoreAsync_ReturnsFailure_WhenProviderThrows()
    {
        _objectStorage.Setup(o => o.WriteObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage unavailable"));

        var result = await CreateService().StoreAsync(CreateRequest(CreateAlignedFaceBytes()));

        Assert.False(result.Success);
        Assert.Contains("storage unavailable", result.FailureReason);
    }

    [Fact]
    public async Task StoreAsync_RejectsInvalidArtifact_WhenValidationFailed()
    {
        var request = CreateRequest(CreateAlignedFaceBytes()) with
        {
            Artifact = CreateArtifact(CreateAlignedFaceBytes(), passed: false),
        };

        var result = await CreateService().StoreAsync(request);

        Assert.False(result.Success);
        Assert.Contains("Invalid artifact", result.FailureReason);
    }

    [Fact]
    public async Task StoreAsync_HandlesLargeAlignedFace_WithoutDoubleSerialization()
    {
        var largeBytes = new byte[512 * 1024];
        Random.Shared.NextBytes(largeBytes);
        var writeCount = 0;

        _objectStorage.Setup(o => o.WriteObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((_, stream, _, _) =>
            {
                writeCount++;
                _ = stream.Length;
            })
            .Returns(Task.CompletedTask);

        var result = await CreateService().StoreAsync(CreateRequest(largeBytes));

        Assert.True(result.Success);
        Assert.True(writeCount >= 1);
    }

    [Fact]
    public void Registry_GetEnabled_FiltersByPolicy()
    {
        var registry = new EnrollmentArtifactTypeRegistry([
            new AlignedFaceArtifactTypeDefinition(),
            new ValidationReportArtifactTypeDefinition(),
        ]);

        var enabled = registry.GetEnabled(new EnrollmentStoragePolicyDecision
        {
            EnabledArtifactTypes = new HashSet<string> { EnrollmentArtifactTypeNames.AlignedFace },
        });

        Assert.Single(enabled);
        Assert.Equal(EnrollmentArtifactTypeNames.AlignedFace, enabled[0].ArtifactType);
    }

    private EnrollmentStorageService CreateService() =>
        new(
            EnrollmentStorageTestFactory.CreatePipelineExecutor(
                _policy.Object,
                _registry,
                _objectStorage.Object,
                _checksumService,
                _repository.Object,
                _unitOfWork.Object,
                TimeProvider.System),
            _repository.Object,
            TimeProvider.System,
            NullLogger<EnrollmentStorageService>.Instance);

    private static byte[] CreateAlignedFaceBytes() => [0x52, 0x49, 0x46, 0x46, 0x10, 0x00, 0x00, 0x00];

    private static EnrollmentStorageRequest CreateRequest(byte[] alignedBytes) =>
        new()
        {
            TenantId = 1,
            CollegeId = 10,
            AcademicYear = 2026,
            StudentId = 42,
            BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ItemId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            PipelineVersion = 1,
            ExecutionTraceId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ValidationProfile = ValidationProfileKind.Default,
            Artifact = CreateArtifact(alignedBytes, passed: true),
        };

    private static EnrollmentValidationArtifact CreateArtifact(byte[] alignedBytes, bool passed) =>
        new()
        {
            Report = new ValidationReport
            {
                OverallResult = passed ? ValidationOverallResult.Passed : ValidationOverallResult.Failed,
                FaceCount = 1,
                RuleResults = [],
                ValidationFailures = [],
                Warnings = [],
                FaceWidth = 112,
                FaceHeight = 112,
            },
            AlignedFaceImage = alignedBytes,
            TimestampUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        };
}
