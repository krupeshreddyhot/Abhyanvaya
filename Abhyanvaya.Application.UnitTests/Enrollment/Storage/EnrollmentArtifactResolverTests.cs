using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Infrastructure.Enrollment.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Storage;

public sealed class EnrollmentArtifactResolverTests
{
    private readonly Mock<IObjectStorageProvider> _objectStorage = new();
    private readonly IChecksumService _checksumService = new Sha256ChecksumService();
    private readonly IEnrollmentArtifactTypeRegistry _registry = EnrollmentStorageTestFactory.CreateRegistry();
    private readonly Mock<IEnrollmentArtifactCache> _cache = new();

    public EnrollmentArtifactResolverTests()
    {
        _objectStorage.Setup(o => o.ProviderName).Returns("Local");
        _cache.Setup(c => c.LookupAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentArtifact?)null);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsAlignedFaceStream_WithMetadata()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var checksum = _checksumService.ComputeSha256Hex(bytes);
        var manifest = CreateManifest(checksum);

        _objectStorage.Setup(o => o.ReadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(bytes));

        var result = await CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = manifest,
            ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Artifact);
        Assert.Equal(EnrollmentArtifactTypeNames.AlignedFace, result.Artifact!.ArtifactType);
        Assert.Equal(checksum, result.Artifact.Checksum);
        Assert.Equal("image/webp", result.Artifact.ContentType);
        Assert.Equal(112, result.Artifact.ImageWidth);

        using var ms = new MemoryStream();
        await result.Artifact.Content.CopyToAsync(ms);
        Assert.Equal(bytes, ms.ToArray());
        await result.Artifact.DisposeAsync();
    }

    [Fact]
    public async Task ResolveAsync_ReturnsValidationReport()
    {
        var bytes = "{\"overallResult\":\"passed\"}"u8.ToArray();
        var checksum = _checksumService.ComputeSha256Hex(bytes);
        var manifest = CreateManifest(checksum, EnrollmentArtifactTypeNames.ValidationReport, "application/json");

        _objectStorage.Setup(o => o.ReadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(bytes));

        var result = await CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = manifest,
            ArtifactType = EnrollmentArtifactTypeNames.ValidationReport,
        });

        Assert.True(result.Success);
        Assert.Equal("application/json", result.Artifact!.ContentType);
        await result.Artifact.DisposeAsync();
    }

    [Fact]
    public async Task ResolveAsync_Fails_WhenArtifactMissing()
    {
        var manifest = CreateManifest("abc");

        var result = await CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = manifest,
            ArtifactType = EnrollmentArtifactTypeNames.ValidationReport,
        });

        Assert.False(result.Success);
        Assert.Equal(EnrollmentArtifactResolveCodes.ArtifactMissing, result.FailureCode);
    }

    [Fact]
    public async Task ResolveAsync_Fails_WhenUnsupportedArtifact()
    {
        var manifest = CreateManifest("abc");

        var result = await CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = manifest,
            ArtifactType = "UnknownType",
        });

        Assert.False(result.Success);
        Assert.Equal(EnrollmentArtifactResolveCodes.UnsupportedArtifact, result.FailureCode);
    }

    [Fact]
    public async Task ResolveAsync_Fails_WhenChecksumMismatch()
    {
        var bytes = new byte[] { 9, 8, 7 };
        var manifest = CreateManifest("deadbeef");

        _objectStorage.Setup(o => o.ReadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(bytes));

        var result = await CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = manifest,
            ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
        });

        Assert.False(result.Success);
        Assert.Equal(EnrollmentArtifactResolveCodes.ChecksumMismatch, result.FailureCode);
    }

    [Fact]
    public async Task ResolveAsync_Fails_WhenStreamUnavailable()
    {
        var manifest = CreateManifest("abc");
        _objectStorage.Setup(o => o.ReadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var result = await CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = manifest,
            ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
        });

        Assert.False(result.Success);
        Assert.Equal(EnrollmentArtifactResolveCodes.StreamUnavailable, result.FailureCode);
    }

    [Fact]
    public async Task ResolveAsync_HandlesLargeArtifact_WithStreamingWrapper()
    {
        var bytes = new byte[11 * 1024 * 1024];
        Random.Shared.NextBytes(bytes);
        var checksum = _checksumService.ComputeSha256Hex(bytes);
        var manifest = CreateManifest(checksum, fileSize: bytes.LongLength);

        _objectStorage.Setup(o => o.ReadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(bytes));

        var result = await CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
        {
            Manifest = manifest,
            ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
        });

        Assert.True(result.Success);
        using var ms = new MemoryStream();
        await result.Artifact!.Content.CopyToAsync(ms);
        Assert.Equal(bytes.Length, ms.Length);
        await result.Artifact.DisposeAsync();
    }

    [Fact]
    public async Task ResolveAsync_RespectsCancellation()
    {
        var manifest = CreateManifest("abc");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _objectStorage.Setup(o => o.ReadObjectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateResolver().ResolveAsync(new EnrollmentArtifactResolveRequest
            {
                Manifest = manifest,
                ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
            }, cts.Token));
    }

    [Fact]
    public void ManifestCompatibility_RejectsFutureVersions()
    {
        var manifest = CreateManifest("abc") with { ManifestVersion = 999 };
        Assert.False(EnrollmentManifestCompatibility.IsCompatible(manifest));
    }

    private EnrollmentArtifactResolver CreateResolver() =>
        new(
            _registry,
            _objectStorage.Object,
            _checksumService,
            _cache.Object,
            new NoOpStorageMetricsCollector(),
            NullLogger<EnrollmentArtifactResolver>.Instance);

    private static EnrollmentStorageManifest CreateManifest(
        string checksum,
        string artifactType = EnrollmentArtifactTypeNames.AlignedFace,
        string contentType = "image/webp",
        long fileSize = 5) =>
        new()
        {
            ManifestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            StorageGroupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            CreatedUtc = DateTimeOffset.UtcNow,
            ManifestVersion = EnrollmentStorageVersions.CurrentManifestVersion,
            SchemaVersion = EnrollmentStorageVersions.ManifestSchemaVersion,
            PipelineVersion = 1,
            ValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion,
            StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
            ArtifactVersion = 1,
            CorrelationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Entries =
            [
                new EnrollmentStorageManifestEntry
                {
                    ArtifactId = Guid.NewGuid(),
                    ArtifactType = artifactType,
                    StorageProvider = "Local",
                    ObjectKey = "internal/key.webp",
                    Checksum = checksum,
                    Version = 1,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    PipelineVersion = 1,
                    ContentType = contentType,
                    ImageMetadata = new EnrollmentStorageImageMetadata
                    {
                        Width = 112,
                        Height = 112,
                        FileSize = fileSize,
                    },
                },
            ],
        };
}
