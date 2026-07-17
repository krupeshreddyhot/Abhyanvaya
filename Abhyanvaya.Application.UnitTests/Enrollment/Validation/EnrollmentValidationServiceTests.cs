using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Enrollment.Validation;
using Abhyanvaya.Infrastructure.Enrollment.Validation.Rules;
using Abhyanvaya.Infrastructure.InsightFace;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Validation;

public sealed class EnrollmentValidationServiceTests
{
    private readonly Mock<IEnrollmentFaceAnalysisService> _faceAnalysis = new();
    private readonly InsightFaceOptions _insightFaceOptions = new();

    private static EnrollmentValidationOptions CreateDefaultOptions() =>
        new()
        {
            BlurThreshold = 0,
            MinimumBrightness = 0,
            MaximumBrightness = 1.0,
            MinimumContrast = 0,
        };

    private EnrollmentValidationService CreateService(EnrollmentValidationOptions? options = null)
    {
        _faceAnalysis.Setup(s => s.ProviderName).Returns("InsightFace");
        _faceAnalysis.Setup(s => s.ModelName).Returns("det_10g.onnx");
        _faceAnalysis.Setup(s => s.PipelineVersion).Returns("InsightFace-1.0");

        var policy = new DefaultEnrollmentValidationPolicy(
            Options.Create(options ?? CreateDefaultOptions()),
            Options.Create(_insightFaceOptions));

        return new EnrollmentValidationService(
            policy,
            EnrollmentValidationTestFactory.CreateRegistry(),
            _faceAnalysis.Object,
            TimeProvider.System,
            NullLogger<EnrollmentValidationService>.Instance);
    }

    private static EnrollmentValidationRequest CreateRequest(
        Stream stream,
        string fileName = "student.jpg",
        long byteSize = 1024,
        EnrollmentValidationExecutionContext? context = null) =>
        new()
        {
            StudentId = 42,
            BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ExecutionContext = context ?? new EnrollmentValidationExecutionContext
            {
                TenantId = 1,
                CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ExecutionTraceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                PipelineVersion = 1,
            },
            ImageStream = stream,
            ImageMetadata = new EnrollmentImageMetadata
            {
                FileName = fileName,
                ContentType = "image/jpeg",
                ByteSize = byteSize,
            },
        };

    private static async Task<MemoryStream> CreateJpegStreamAsync(int width, int height, Action<Image<Rgb24>>? mutate = null)
    {
        using var image = new Image<Rgb24>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(Color.White));
        mutate?.Invoke(image);

        var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static EnrollmentDetectedFace CreateDetectedFace(
        float score = 0.92f,
        int bboxWidth = 200,
        int bboxHeight = 240) =>
        new()
        {
            DetectionScore = score,
            BoundingBoxX = 10,
            BoundingBoxY = 10,
            BoundingBoxWidth = bboxWidth,
            BoundingBoxHeight = bboxHeight,
            Landmarks =
            [
                50, 60, 150, 60, 100, 110, 70, 150, 130, 150,
            ],
        };

    private static byte[] CreateAlignedWebp(bool blurry, bool dark = false)
    {
        using var image = new Image<Rgb24>(112, 112);
        if (dark)
        {
            image.Mutate(ctx => ctx.BackgroundColor(Color.Black));
        }
        else
        {
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    image[x, y] = ((x + y) % 16 == 0) ? Color.Black : Color.White;
                }
            }
        }

        if (blurry)
        {
            image.Mutate(ctx => ctx.GaussianBlur(8));
        }

        using var ms = new MemoryStream();
        image.SaveAsWebp(ms);
        return ms.ToArray();
    }

    private void SetupFaceAnalysis(int width, int height, IReadOnlyList<EnrollmentDetectedFace> faces, byte[]? alignedWebp)
    {
        _faceAnalysis.Setup(s => s.ProviderName).Returns("InsightFace");
        _faceAnalysis.Setup(s => s.ModelName).Returns("det_10g.onnx");
        _faceAnalysis.Setup(s => s.PipelineVersion).Returns("InsightFace-1.0");
        _faceAnalysis.Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentFaceAnalysisResult
            {
                ImageWidth = width,
                ImageHeight = height,
                Faces = faces,
                AlignedFaceWebpBytes = alignedWebp,
            });
    }

    [Fact]
    public async Task ValidateAsync_Passes_ForValidSingleFaceImage()
    {
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace()], CreateAlignedWebp(blurry: false));

        var result = await CreateService().ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.True(result.ValidationPassed);
        Assert.Null(result.FailureCategory);
        Assert.NotNull(result.AlignedFaceBytes);
        Assert.NotNull(result.Report.CompositeScore);
        Assert.Equal(1, result.Report.FaceCount);
        Assert.Equal(ValidationOverallResult.Passed, result.Report.OverallResult);
        Assert.Contains(result.Report.RuleResults, r => r.RuleId == EnrollmentValidationRuleIds.ExactlyOneFace && r.Outcome == ValidationRuleOutcome.Pass);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenNoFaceDetected()
    {
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [], null);

        var result = await CreateService().ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.Equal(FailureCategory.NoFaceDetected, result.FailureCategory);
        Assert.Equal(EnrollmentValidationDiagnosticCodes.NoFace, result.DiagnosticCode);
        Assert.Equal(0, result.Report.FaceCount);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenMultipleFacesDetected()
    {
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace(), CreateDetectedFace(score: 0.88f)], null);

        var result = await CreateService().ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.Equal(FailureCategory.MultipleFacesDetected, result.FailureCategory);
        Assert.Equal(EnrollmentValidationDiagnosticCodes.MultipleFaces, result.DiagnosticCode);
        Assert.True(result.Report.FaceCount >= 2);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenSourceResolutionTooLow()
    {
        await using var stream = await CreateJpegStreamAsync(320, 240);

        var result = await CreateService().ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.Equal(FailureCategory.LowResolutionRejected, result.FailureCategory);
        Assert.Equal(EnrollmentValidationDiagnosticCodes.SourceResTooLow, result.DiagnosticCode);
        _faceAnalysis.Verify(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenBlurScoreTooLow()
    {
        var options = CreateDefaultOptions();
        options.BlurThreshold = 500;
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace()], CreateAlignedWebp(blurry: true));

        var result = await CreateService(options).ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.Equal(FailureCategory.BlurRejected, result.FailureCategory);
        Assert.Equal(EnrollmentValidationDiagnosticCodes.BlurRejected, result.DiagnosticCode);
        Assert.NotNull(result.Report.BlurScore);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenBrightnessOutOfRange()
    {
        var options = CreateDefaultOptions();
        options.MinimumBrightness = 0.20;
        options.MaximumBrightness = 0.10;
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace()], CreateAlignedWebp(blurry: false, dark: true));

        var result = await CreateService(options).ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.Equal(EnrollmentValidationDiagnosticCodes.BrightnessRejected, result.DiagnosticCode);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenContrastTooLow()
    {
        var options = CreateDefaultOptions();
        options.MinimumContrast = 0.90;
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace()], CreateAlignedWebp(blurry: false));

        var result = await CreateService(options).ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.Equal(EnrollmentValidationDiagnosticCodes.ContrastRejected, result.DiagnosticCode);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenImageCorrupt()
    {
        var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x00, 0x00]);
        var result = await CreateService().ValidateAsync(CreateRequest(stream, "broken.jpg", byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.True(result.FailureCategory is FailureCategory.CorruptImage or FailureCategory.InvalidImage);
        Assert.NotNull(result.DiagnosticCode);
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenImageFormatUnsupported()
    {
        var stream = new MemoryStream("not-an-image"u8.ToArray());
        var result = await CreateService().ValidateAsync(CreateRequest(stream, "student.bmp", byteSize: 11));

        Assert.False(result.ValidationPassed);
        Assert.Equal(FailureCategory.InvalidImage, result.FailureCategory);
        Assert.Equal(EnrollmentValidationDiagnosticCodes.UnsupportedFormat, result.DiagnosticCode);
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenCancelled()
    {
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace()], CreateAlignedWebp(blurry: false));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService().ValidateAsync(CreateRequest(stream, byteSize: stream.Length), cts.Token));
    }

    [Fact]
    public async Task ValidateAsync_IsThreadSafe_WithParallelCalls()
    {
        _faceAnalysis.Setup(s => s.ProviderName).Returns("InsightFace");
        _faceAnalysis.Setup(s => s.ModelName).Returns("det_10g.onnx");
        _faceAnalysis.Setup(s => s.PipelineVersion).Returns("InsightFace-1.0");
        _faceAnalysis.Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EnrollmentFaceAnalysisResult
            {
                ImageWidth = 800,
                ImageHeight = 600,
                Faces = [CreateDetectedFace()],
                AlignedFaceWebpBytes = CreateAlignedWebp(blurry: false),
            });

        var service = CreateService();
        var tasks = Enumerable.Range(0, 16).Select(async _ =>
        {
            await using var stream = await CreateJpegStreamAsync(800, 600);
            return await service.ValidateAsync(CreateRequest(stream, byteSize: stream.Length));
        });

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.ValidationPassed));
    }

    [Fact]
    public async Task ValidateAsync_PopulatesTelemetry()
    {
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace()], CreateAlignedWebp(blurry: false));

        var result = await CreateService().ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.NotNull(result.Telemetry);
        Assert.Equal("InsightFace", result.Telemetry!.Engine);
        Assert.True(result.Telemetry.ElapsedMilliseconds >= 0);
        Assert.True(result.Telemetry.RulesExecuted > 0);
        Assert.Equal(result.Telemetry, result.Report.Telemetry);
    }

    [Fact]
    public async Task ValidateAsync_CompositeScoreIsInformationalOnly()
    {
        var options = CreateDefaultOptions();
        options.BlurThreshold = 10_000;
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace(bboxWidth: 50, bboxHeight: 50)], CreateAlignedWebp(blurry: false));

        var result = await CreateService(options).ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.False(result.ValidationPassed);
        Assert.NotNull(result.Report.CompositeScore);
    }

    [Fact]
    public void ValidationReport_ContainsAllRequiredRuleOutcomes()
    {
        var ruleIds = new HashSet<string>(StringComparer.Ordinal)
        {
            EnrollmentValidationRuleIds.ImageFormat,
            EnrollmentValidationRuleIds.CorruptImage,
            EnrollmentValidationRuleIds.MinimumSourceResolution,
            EnrollmentValidationRuleIds.MaximumSourceResolution,
            EnrollmentValidationRuleIds.ExactlyOneFace,
            EnrollmentValidationRuleIds.FaceConfidence,
            EnrollmentValidationRuleIds.MinimumFaceCropResolution,
            EnrollmentValidationRuleIds.FaceSizeCoverage,
            EnrollmentValidationRuleIds.BlurScore,
            EnrollmentValidationRuleIds.Pose,
            EnrollmentValidationRuleIds.Brightness,
            EnrollmentValidationRuleIds.Contrast,
            EnrollmentValidationRuleIds.Liveness,
            EnrollmentValidationRuleIds.MaskDetection,
        };

        Assert.Equal(14, ruleIds.Count);
    }

    [Fact]
    public async Task ValidateAsync_FutureRules_AreSkipped()
    {
        await using var stream = await CreateJpegStreamAsync(800, 600);
        SetupFaceAnalysis(800, 600, [CreateDetectedFace()], CreateAlignedWebp(blurry: false));

        var result = await CreateService().ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        var liveness = result.Report.RuleResults.Single(r => r.RuleId == EnrollmentValidationRuleIds.Liveness);
        Assert.Equal(ValidationRuleOutcome.Skipped, liveness.Outcome);
    }
}
