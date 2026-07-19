using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.FaceEnrollment;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.ArtifactStorage;
using Abhyanvaya.Infrastructure.FaceEnrollment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.ArtifactStorage;

public sealed class ArtifactStoragePlatformTests
{
    [Fact]
    public void ArtifactIntegrityService_ValidatesChecksum()
    {
        var service = new ArtifactIntegrityService();
        var content = new byte[] { 1, 2, 3, 4 };
        var checksum = service.ComputeSha256(content);

        Assert.True(service.ValidateChecksum(checksum, checksum));
        Assert.False(service.ValidateChecksum(checksum, "INVALID"));
    }

    [Fact]
    public async Task ArtifactUploadQueue_EnqueuesRequests()
    {
        var queue = new ArtifactUploadQueue();
        await queue.EnqueueAsync(CreateRequest(1));
        Assert.Equal(1, queue.QueueDepth);
    }

    [Fact]
    public async Task ArtifactUploadCoordinator_UploadsAndVerifiesAllItems()
    {
        var storage = new InMemoryArtifactStorageProvider();
        var registry = new InMemoryArtifactRegistryRepository();
        var manifestRepository = new InMemoryArtifactManifestRepository();
        var coordinator = CreateCoordinator(storage, registry, manifestRepository);

        var request = CreateRequest(42);
        var result = await coordinator.ProcessQueuedItemAsync(request);

        Assert.True(result.Results.All(r => r.Verified));
        Assert.Equal(5, result.Results.Count);
        Assert.Equal(5, registry.Records.Count);
        Assert.NotNull(await manifestRepository.GetManifestAsync(request.Artifact.ManifestId));
    }

    [Fact]
    public async Task ArtifactUploadCoordinator_ProcessesParallelRequests()
    {
        var storage = new InMemoryArtifactStorageProvider();
        var registry = new InMemoryArtifactRegistryRepository();
        var manifestRepository = new InMemoryArtifactManifestRepository();
        var coordinator = CreateCoordinator(storage, registry, manifestRepository);

        var tasks = Enumerable.Range(1, 200)
            .Select(i => coordinator.ProcessQueuedItemAsync(CreateRequest(i)))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Equal(200, results.Length);
        Assert.Equal(200 * 5, registry.Records.Count);
    }

    [Fact]
    public async Task ArtifactVerificationService_FailsOnChecksumMismatch()
    {
        var storage = new InMemoryArtifactStorageProvider();
        var service = new ArtifactVerificationService(
            storage,
            new ArtifactIntegrityService(),
            new ConfigurableArtifactVerificationPolicy(Options.Create(new ArtifactVerificationPolicyOptions())));

        var content = new byte[] { 9, 9, 9 };
        var context = new ArtifactStorageContext
        {
            ArtifactId = Guid.NewGuid(),
            EnrollmentId = Guid.NewGuid(),
            StudentId = 1,
            BatchId = Guid.NewGuid(),
            StorageProvider = "memory",
            Bucket = "test",
            StorageKey = "key",
            Checksum = "BAD",
            CorrelationId = Guid.NewGuid(),
            TraceId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
        };

        var metadata = new ArtifactMetadata
        {
            ArtifactType = "aligned-face",
            ContentType = "image/jpeg",
            FileSize = content.LongLength,
            Checksum = "BAD",
            Version = "1.0",
            CreatedUtc = DateTime.UtcNow,
            RetentionPolicy = "STANDARD",
            StorageClass = "STANDARD",
        };

        var result = await service.VerifyAsync(context, metadata, content);
        Assert.False(result.Passed);
    }

    [Fact]
    public void ArtifactRetryPolicy_UsesExponentialBackoff()
    {
        var policy = new ConfigurableArtifactRetryPolicy(Options.Create(new ArtifactRetryPolicyOptions
        {
            InitialDelayMilliseconds = 100,
            BackoffMultiplier = 2,
            MaxDelayMilliseconds = 1000,
        }));

        Assert.True(policy.GetDelay(1) < policy.GetDelay(3));
        Assert.True(policy.ShouldRetry(new InvalidOperationException(), 1));
        Assert.False(policy.ShouldRetry(new InvalidOperationException(), policy.MaximumRetries));
    }

    private static ArtifactUploadCoordinator CreateCoordinator(
        IArtifactStorageProvider storage,
        IArtifactRegistryRepository registry,
        IArtifactManifestRepository manifestRepository)
    {
        var queue = new ArtifactUploadQueue();
        var tracing = new Mock<IAITracingService>();
        tracing.Setup(t => t.CreateContext(It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<string>()))
            .Returns(new Application.AIOperations.AITraceContext
            {
                TraceId = Guid.NewGuid(),
                CorrelationId = Guid.NewGuid(),
                CurrentSpanId = Guid.NewGuid(),
            });
        tracing.Setup(t => t.StartSpan(It.IsAny<Application.AIOperations.AITraceContext>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((Application.AIOperations.AITraceContext ctx, string _, string __) => ctx);

        return new ArtifactUploadCoordinator(
            queue,
            new ArtifactUploadService(
                storage,
                new ArtifactIntegrityService(),
                new ConfigurableArtifactRetryPolicy(Options.Create(new ArtifactRetryPolicyOptions())),
                registry,
                Options.Create(new ArtifactStorageOptions()),
                NullLogger<ArtifactUploadService>.Instance),
            new ArtifactVerificationService(
                storage,
                new ArtifactIntegrityService(),
                new ConfigurableArtifactVerificationPolicy(Options.Create(new ArtifactVerificationPolicyOptions()))),
            new ArtifactIntegrityService(),
            new ArtifactVersionManager(Options.Create(new ArtifactStorageOptions())),
            registry,
            manifestRepository,
            storage,
            Mock.Of<IAITelemetryService>(),
            tracing.Object,
            Options.Create(new ArtifactStorageOptions()),
            NullLogger<ArtifactUploadCoordinator>.Instance);
    }

    private static ArtifactUploadRequest CreateRequest(int studentId)
    {
        var manifestId = Guid.NewGuid();
        var aligned = new byte[] { 10, 20, 30, 40 };
        return new ArtifactUploadRequest
        {
            Artifact = new EnrollmentArtifact
            {
                StudentId = studentId,
                PhotoReference = $"photo://{studentId}",
                AlignedPhotoReference = $"aligned://{studentId}",
                EmbeddingReference = $"embedding://{studentId}",
                EmbeddingDimension = 4,
                EmbeddingVersion = "1.0",
                QualityScore = 0.95m,
                ManifestId = manifestId,
                EnrollmentVersion = "1.0",
                CreatedUtc = DateTime.UtcNow,
                Checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(aligned)),
            },
            EnrollmentId = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            PhotoId = Guid.NewGuid(),
            TenantId = 1,
            CorrelationId = Guid.NewGuid(),
            TraceId = Guid.NewGuid(),
            OriginalPhotoBytes = [1, 2, 3],
            AlignedFaceBytes = aligned,
            Embedding = [1f, 0f, 0f, 0f],
            OriginalContentType = "image/jpeg",
        };
    }

    private sealed class InMemoryArtifactStorageProvider : IArtifactStorageProvider
    {
        private readonly Dictionary<string, (long Size, string Checksum)> _objects = new(StringComparer.Ordinal);

        public string ProviderName => "memory";
        public string Bucket => "test-bucket";

        public Task UploadAsync(string storageKey, Stream content, ArtifactMetadata metadata, CancellationToken cancellationToken = default)
        {
            _objects[storageKey] = (metadata.FileSize, metadata.Checksum);
            return Task.CompletedTask;
        }

        public Task<bool> VerifyExistsAsync(string storageKey, long expectedLength, CancellationToken cancellationToken = default) =>
            Task.FromResult(_objects.TryGetValue(storageKey, out var obj) && obj.Size == expectedLength);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            _objects.Remove(storageKey);
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryArtifactRegistryRepository : IArtifactRegistryRepository
    {
        public List<ArtifactRegistryRecord> Records { get; } = [];

        public Task SaveAsync(ArtifactRegistryRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Guid artifactId, ArtifactUploadState status, string? verificationJson = null, string? failureReason = null, CancellationToken cancellationToken = default)
        {
            var record = Records.First(x => x.Id == artifactId);
            Records.Remove(record);
            Records.Add(record with { Status = status });
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ArtifactRegistryRecord>> GetByBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArtifactRegistryRecord>>(Records.Where(x => x.BatchId == batchId).ToList());

        public Task<IReadOnlyList<ArtifactRegistryRecord>> GetEligibleForArchiveAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArtifactRegistryRecord>>(Records.Where(x => x.CreatedUtc <= cutoffUtc).ToList());

        public Task<IReadOnlyList<ArtifactRegistryRecord>> GetEligibleForDeleteAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ArtifactRegistryRecord>>(Records.Where(x => x.CreatedUtc <= cutoffUtc).ToList());
    }

    private sealed class InMemoryArtifactManifestRepository : IArtifactManifestRepository
    {
        private readonly Dictionary<Guid, ArtifactStorageManifestRecord> _manifests = new();

        public Task SaveManifestAsync(ArtifactStorageManifestRecord record, CancellationToken cancellationToken = default)
        {
            _manifests[record.Id] = record;
            return Task.CompletedTask;
        }

        public Task<ArtifactStorageManifestRecord?> GetManifestAsync(Guid manifestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_manifests.TryGetValue(manifestId, out var record) ? record : null);

        public Task UpdateManifestStatusAsync(Guid manifestId, ArtifactUploadState status, CancellationToken cancellationToken = default)
        {
            if (_manifests.TryGetValue(manifestId, out var record))
            {
                _manifests[manifestId] = record with { Status = status };
            }

            return Task.CompletedTask;
        }
    }
}
