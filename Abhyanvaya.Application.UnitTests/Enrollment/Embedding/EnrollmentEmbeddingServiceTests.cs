using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Embedding;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Infrastructure.Enrollment.Embedding;
using Abhyanvaya.Infrastructure.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Embedding;

public sealed class EnrollmentEmbeddingServiceTests
{
    private readonly Mock<IEnrollmentArtifactResolver> _artifactResolver = new();
    private readonly Mock<IEmbeddingEngine> _embeddingEngine = new();
    private readonly IEmbeddingValidator _validator = new EmbeddingValidator();
    private readonly IEmbeddingNormalizer _normalizer = new EmbeddingNormalizer();
    private readonly IEmbeddingQualityAnalyzer _qualityAnalyzer = new EmbeddingQualityAnalyzer();
    private readonly Mock<IEmbeddingGenerationMetrics> _metrics = new();

    public EnrollmentEmbeddingServiceTests()
    {
        _embeddingEngine.Setup(e => e.EngineName).Returns("InsightFace");
        _embeddingEngine.Setup(e => e.ModelName).Returns("w600k_r50.onnx");
        _embeddingEngine.Setup(e => e.ModelVersion).Returns("InsightFace-1.0");
        _embeddingEngine.Setup(e => e.ExpectedDimension).Returns(512);
        _embeddingEngine.Setup(e => e.NormalizationMethod).Returns("L2");
    }

    [Fact]
    public async Task GenerateAsync_ReturnsEmbeddingArtifact_WhenPipelineSucceeds()
    {
        SetupSuccessfulResolve();
        SetupEngineReturns(NormalizedVector512());

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.Artifact);
        Assert.Equal(512, result.Artifact!.EmbeddingDimension);
        Assert.Equal("w600k_r50.onnx", result.Artifact.EmbeddingModel);
        Assert.NotNull(result.Metadata);
        Assert.Equal("L2", result.Metadata!.Normalization);
        Assert.NotNull(result.Statistics);
        Assert.True(result.Statistics!.IsNormalized);
        Assert.NotNull(result.Telemetry);
        Assert.True(result.Telemetry!.TotalDuration >= TimeSpan.Zero);
        _metrics.Verify(m => m.RecordSuccess(
            "InsightFace",
            "w600k_r50.onnx",
            512,
            It.IsAny<long>(),
            It.IsAny<long>(),
            It.IsAny<long>(),
            0), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsMissingArtifact_WhenResolverFails()
    {
        _artifactResolver.Setup(r => r.ResolveAsync(It.IsAny<EnrollmentArtifactResolveRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentArtifactResolveResult.Failed(
                EnrollmentArtifactResolveCodes.ArtifactMissing,
                "Aligned face not found.",
                TimeSpan.FromMilliseconds(5)));

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentEmbeddingFailureCodes.ArtifactMissing, result.FailureCode);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsInvalidDimension_WhenVectorLengthMismatch()
    {
        SetupSuccessfulResolve();
        SetupEngineReturns(Enumerable.Repeat(0.1f, 128).ToArray());

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentEmbeddingFailureCodes.InvalidDimension, result.FailureCode);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsInvalidVector_WhenVectorContainsNaN()
    {
        SetupSuccessfulResolve();
        var vector = NormalizedVector512();
        vector[10] = float.NaN;
        SetupEngineReturns(vector);

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentEmbeddingFailureCodes.InvalidVector, result.FailureCode);
    }

    [Fact]
    public async Task GenerateAsync_NormalizesVector_ToUnitMagnitude()
    {
        SetupSuccessfulResolve();
        var raw = Enumerable.Repeat(2f, 512).ToArray();
        SetupEngineReturns(raw);

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.True(result.Success);
        var magnitude = ComputeMagnitude(result.Artifact!.EmbeddingVector);
        Assert.InRange(magnitude, 0.99f, 1.01f);
    }

    [Fact]
    public async Task GenerateAsync_SupportsConcurrentExecution()
    {
        SetupSuccessfulResolve();
        SetupEngineReturns(NormalizedVector512());

        var service = CreateService();
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => service.GenerateAsync(CreateRequest()))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task GenerateAsync_PropagatesCancellation()
    {
        SetupSuccessfulResolve();
        _embeddingEngine.Setup(e => e.GenerateFromAlignedFaceAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new EmbeddingEngineResult([], 0);
            });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService().GenerateAsync(CreateRequest(), cts.Token));
    }

    [Fact]
    public async Task GenerateAsync_HandlesLargeAlignedFaceStream()
    {
        var largeBytes = new byte[512 * 1024];
        Random.Shared.NextBytes(largeBytes);
        SetupSuccessfulResolve(largeBytes);
        SetupEngineReturns(NormalizedVector512());

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.True(result.Success);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsEmbeddingFailure_WhenEngineThrows()
    {
        SetupSuccessfulResolve();
        _embeddingEngine.Setup(e => e.GenerateFromAlignedFaceAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Corrupt image payload."));

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.False(result.Success);
        Assert.Equal(EnrollmentEmbeddingFailureCodes.EmbeddingFailure, result.FailureCode);
        Assert.Contains("Corrupt image", result.FailureReason);
    }

    [Fact]
    public async Task GenerateAsync_PopulatesTelemetryDurations()
    {
        SetupSuccessfulResolve();
        _embeddingEngine.Setup(e => e.GenerateFromAlignedFaceAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingEngineResult(NormalizedVector512(), 25));

        var result = await CreateService().GenerateAsync(CreateRequest());

        Assert.True(result.Success);
        Assert.True(result.Telemetry!.ResolveDuration >= TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMilliseconds(25), result.Telemetry.InferenceDuration);
        Assert.True(result.Telemetry.NormalizationDuration >= TimeSpan.Zero);
        Assert.True(result.Telemetry.ValidationDuration >= TimeSpan.Zero);
        Assert.True(result.Telemetry.TotalDuration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task GenerateAsync_RejectsUnsupportedArtifactType()
    {
        var request = CreateRequest() with { ArtifactType = EnrollmentArtifactTypeNames.ValidationReport };

        var result = await CreateService().GenerateAsync(request);

        Assert.False(result.Success);
        Assert.Equal(EnrollmentEmbeddingFailureCodes.UnsupportedArtifact, result.FailureCode);
        _artifactResolver.Verify(
            r => r.ResolveAsync(It.IsAny<EnrollmentArtifactResolveRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Validator_DetectsZeroVector()
    {
        var result = _validator.Validate(new float[512]);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void QualityAnalyzer_ProducesDiagnosticsWithoutRejecting()
    {
        var vector = NormalizedVector512();
        var stats = _validator.ComputeStatistics(vector);
        var analysis = _qualityAnalyzer.Analyze(vector, stats);

        Assert.InRange(analysis.QualityScore, 0f, 1f);
        Assert.NotNull(analysis.Diagnostics);
    }

    private void SetupSuccessfulResolve(byte[]? bytes = null)
    {
        bytes ??= [0x01, 0x02, 0x03, 0x04];
        var artifact = new EnrollmentArtifact
        {
            ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
            Content = new MemoryStream(bytes),
            Metadata = new EnrollmentArtifactMetadata
            {
                ArtifactId = Guid.NewGuid(),
                ManifestId = Guid.NewGuid(),
                ManifestVersion = 1,
                PipelineVersion = 1,
                StorageVersion = 1,
                ValidationVersion = 1,
            },
            ContentType = "image/webp",
            Checksum = "abc",
            ImageWidth = 112,
            ImageHeight = 112,
            Version = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        _artifactResolver.Setup(r => r.ResolveAsync(It.IsAny<EnrollmentArtifactResolveRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EnrollmentArtifactResolveResult.Succeeded(artifact, TimeSpan.FromMilliseconds(3)));
    }

    private void SetupEngineReturns(float[] vector)
    {
        _embeddingEngine.Setup(e => e.GenerateFromAlignedFaceAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingEngineResult(vector, 12));
    }

    private EnrollmentEmbeddingService CreateService() =>
        new(
            _artifactResolver.Object,
            _embeddingEngine.Object,
            _validator,
            _normalizer,
            _qualityAnalyzer,
            _metrics.Object,
            TimeProvider.System,
            NullLogger<EnrollmentEmbeddingService>.Instance);

    private static EnrollmentEmbeddingRequest CreateRequest()
    {
        var manifestId = Guid.NewGuid();
        return new EnrollmentEmbeddingRequest
        {
            StudentId = 42,
            BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Manifest = new EnrollmentStorageManifest
            {
                ManifestId = manifestId,
                StorageGroupId = manifestId,
                Entries =
                [
                    new EnrollmentStorageManifestEntry
                    {
                        ArtifactId = Guid.NewGuid(),
                        ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
                        StorageProvider = "Local",
                        ObjectKey = "tenant/student/AlignedFace/v1.webp",
                        Checksum = "abc",
                        Version = 1,
                        CreatedUtc = DateTimeOffset.UtcNow,
                        PipelineVersion = 1,
                        ContentType = "image/webp",
                    },
                ],
                CreatedUtc = DateTimeOffset.UtcNow,
                ManifestVersion = 1,
                SchemaVersion = 1,
                PipelineVersion = 1,
                ValidationVersion = 1,
                StorageVersion = 1,
                ArtifactVersion = 1,
                CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            },
        };
    }

    private static float[] NormalizedVector512()
    {
        var vector = new float[512];
        Array.Fill(vector, 1f / MathF.Sqrt(512));
        return vector;
    }

    private static float ComputeMagnitude(IReadOnlyList<float> vector)
    {
        var sum = 0f;
        for (var i = 0; i < vector.Count; i++)
        {
            sum += vector[i] * vector[i];
        }

        return MathF.Sqrt(sum);
    }
}
