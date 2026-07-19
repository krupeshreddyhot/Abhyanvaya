using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
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

public sealed class EnrollmentValidationFrameworkTests
{
    [Fact]
    public void RuleRegistry_OrdersRulesByOrderThenName()
    {
        var registry = EnrollmentValidationTestFactory.CreateRegistry();
        var ordered = registry.GetOrderedRules(ValidationProfiles.Default);

        Assert.Equal(EnrollmentValidationRuleIds.ImageFormat, ordered[0].Name);
        Assert.True(ordered.Zip(ordered.Skip(1)).All(pair => pair.First.Order <= pair.Second.Order));
    }

    [Fact]
    public void RuleRegistry_DisabledFutureRulesAreSkippedByProfile()
    {
        var registry = EnrollmentValidationTestFactory.CreateRegistry();
        Assert.False(registry.IsRuleEnabled(EnrollmentValidationRuleIds.Liveness, ValidationProfiles.Default));
        Assert.True(registry.IsRuleEnabled(EnrollmentValidationRuleIds.BlurScore, ValidationProfiles.Default));
    }

    [Fact]
    public void RuleRegistry_Register_AddsCustomRule()
    {
        var registry = EnrollmentValidationTestFactory.CreateRegistry();
        var custom = new TestValidationRule("CustomRule", 5);
        registry.Register(custom);

        var ordered = registry.GetOrderedRules(ValidationProfiles.Default);
        Assert.Contains(ordered, r => r.Name == "CustomRule");
    }

    [Fact]
    public async Task Policy_DefaultProfile_UsesConfiguredOptions()
    {
        var policy = new DefaultEnrollmentValidationPolicy(
            Options.Create(new EnrollmentValidationOptions { BlurThreshold = 222 }),
            Options.Create(new InsightFaceOptions()));

        var decision = await policy.ResolveAsync(new EnrollmentValidationPolicyRequest { TenantId = 1 });

        Assert.Equal(ValidationProfileKind.Default, decision.Profile.Kind);
        Assert.Equal(222, decision.Thresholds.BlurThreshold);
    }

    [Fact]
    public async Task Policy_StrictProfile_AppliesStricterBlurThreshold()
    {
        var policy = new DefaultEnrollmentValidationPolicy(
            Options.Create(new EnrollmentValidationOptions()),
            Options.Create(new InsightFaceOptions()));

        var decision = await policy.ResolveAsync(new EnrollmentValidationPolicyRequest
        {
            TenantId = 1,
            RequestedProfile = ValidationProfileKind.Strict,
        });

        Assert.Equal(150, decision.Thresholds.BlurThreshold);
    }

    [Fact]
    public async Task Pipeline_WarningEnforcement_DoesNotRejectEnrollment()
    {
        var registry = EnrollmentValidationTestFactory.CreateRegistry();
        var executor = new EnrollmentValidationPipelineExecutor(registry);
        var policy = await new DefaultEnrollmentValidationPolicy(
            Options.Create(new EnrollmentValidationOptions { BlurThreshold = 999_999 }),
            Options.Create(new InsightFaceOptions()))
            .ResolveAsync(new EnrollmentValidationPolicyRequest
            {
                TenantId = 1,
                RequestedProfile = ValidationProfileKind.Default,
            });

        policy = policy with
        {
            SeverityOverrides = new Dictionary<string, ValidationRuleEnforcement>(StringComparer.Ordinal)
            {
                [EnrollmentValidationRuleIds.BlurScore] = ValidationRuleEnforcement.Warning,
            },
        };

        var accessor = new TestAccessorWithMetrics(blurScore: 1);
        var context = new EnrollmentValidationRuleContext
        {
            Request = CreateMinimalRequest(),
            Policy = policy,
            Thresholds = policy.Thresholds,
            AnalysisAccessor = accessor,
        };

        var results = await executor.ExecuteAsync(context, CancellationToken.None);
        var blur = results.Single(r => r.RuleName == EnrollmentValidationRuleIds.BlurScore);

        Assert.Equal(ValidationRuleOutcome.Warning, blur.Severity);
        Assert.True(blur.Passed);
        Assert.True(EnrollmentValidationPipelineExecutor.ResolveEligibility(results).Passed);
    }

    [Fact]
    public async Task ValidateAsync_CreatesArtifact_OnSuccess()
    {
        await using var stream = await CreateJpegStreamAsync(800, 600);
        var service = CreateIntegrationService();
        SetupHappyFaceAnalysis(800, 600);

        var result = await service.ValidateAsync(CreateRequest(stream, byteSize: stream.Length));

        Assert.NotNull(result.Artifact);
        Assert.Equal(result.Report.CompositeScore, result.Artifact!.QualityScore);
        Assert.NotNull(result.Artifact.DiagnosticImages);
    }

    [Fact]
    public async Task NoOpValidationCache_ReturnsNullLookupAsync()
    {
        IValidationCache cache = new NoOpValidationCache();
        var artifact = await cache.LookupAsync("key", CancellationToken.None);
        Assert.Null(artifact);
    }

    private static EnrollmentValidationRequest CreateMinimalRequest() =>
        new()
        {
            StudentId = 1,
            BatchId = Guid.NewGuid(),
            ExecutionContext = new EnrollmentValidationExecutionContext
            {
                TenantId = 1,
                CorrelationId = Guid.NewGuid(),
                ExecutionTraceId = Guid.NewGuid(),
                PipelineVersion = 1,
            },
            ImageStream = Stream.Null,
            ImageMetadata = new EnrollmentImageMetadata
            {
                FileName = "x.jpg",
                ByteSize = 1,
            },
        };

    private sealed class TestValidationRule : EnrollmentValidationRuleBase
    {
        private readonly string _name;
        private readonly int _order;

        public TestValidationRule(string name, int order)
        {
            _name = name;
            _order = order;
        }

        public override string Name => _name;
        public override int Order => _order;

        protected override Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
            EnrollmentValidationRuleContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(Pass());
    }

    private sealed class TestAccessorWithMetrics : IEnrollmentFaceAnalysisAccessor
    {
        private readonly float _blurScore;

        public TestAccessorWithMetrics(float blurScore) => _blurScore = blurScore;

        public EnrollmentImageIntegrityCheckerResult? FormatResult { get; } =
            new() { IsValid = true };

        public bool IsDetectionSkipped => false;

        public void MarkDetectionSkipped()
        {
        }

        public EnrollmentImageIntegrityCheckerResult ValidateFormat() =>
            FormatResult!;

        public Task<EnrollmentImageIntegrityCheckerResult?> GetDecodeResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult<EnrollmentImageIntegrityCheckerResult?>(new EnrollmentImageIntegrityCheckerResult
            {
                IsValid = true,
                Width = 800,
                Height = 600,
            });

        public Task<EnrollmentFaceAnalysisResult?> GetAnalysisAsync(CancellationToken cancellationToken) =>
            Task.FromResult<EnrollmentFaceAnalysisResult?>(new EnrollmentFaceAnalysisResult
            {
                ImageWidth = 800,
                ImageHeight = 600,
                Faces =
                [
                    new EnrollmentDetectedFace
                    {
                        DetectionScore = 0.95f,
                        BoundingBoxX = 10,
                        BoundingBoxY = 10,
                        BoundingBoxWidth = 200,
                        BoundingBoxHeight = 240,
                        Landmarks = [50, 60, 150, 60, 100, 110, 70, 150, 130, 150],
                    },
                ],
                AlignedFaceWebpBytes = [1, 2, 3],
            });

        public Task<FaceQualityMetrics?> GetQualityMetricsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<FaceQualityMetrics?>(new FaceQualityMetrics
            {
                BlurScore = _blurScore,
                Brightness = 0.5f,
                Contrast = 0.2f,
                Pose = new PoseEstimate { Yaw = 0, Pitch = 0, Roll = 0, Deviation = 0 },
            });
    }

    // Reuse helpers from service tests via minimal duplication for integration path
    private static async Task<MemoryStream> CreateJpegStreamAsync(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(Color.White));
        var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static EnrollmentValidationRequest CreateRequest(Stream stream, long byteSize) =>
        new()
        {
            StudentId = 42,
            BatchId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ExecutionContext = new EnrollmentValidationExecutionContext
            {
                TenantId = 1,
                CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ExecutionTraceId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                PipelineVersion = 1,
            },
            ImageStream = stream,
            ImageMetadata = new EnrollmentImageMetadata
            {
                FileName = "student.jpg",
                ContentType = "image/jpeg",
                ByteSize = byteSize,
            },
        };

    private readonly Mock<IEnrollmentFaceAnalysisService> _faceAnalysis = new();

    private void SetupHappyFaceAnalysis(int width, int height)
    {
        _faceAnalysis.Setup(s => s.ProviderName).Returns("InsightFace");
        _faceAnalysis.Setup(s => s.ModelName).Returns("det_10g.onnx");
        _faceAnalysis.Setup(s => s.PipelineVersion).Returns("InsightFace-1.0");
        _faceAnalysis.Setup(s => s.AnalyzeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnrollmentFaceAnalysisResult
            {
                ImageWidth = width,
                ImageHeight = height,
                Faces =
                [
                    new EnrollmentDetectedFace
                    {
                        DetectionScore = 0.92f,
                        BoundingBoxX = 10,
                        BoundingBoxY = 10,
                        BoundingBoxWidth = 200,
                        BoundingBoxHeight = 240,
                        Landmarks = [50, 60, 150, 60, 100, 110, 70, 150, 130, 150],
                    },
                ],
                AlignedFaceWebpBytes = CreateAlignedWebp(),
            });
    }

    private static byte[] CreateAlignedWebp()
    {
        using var image = new Image<Rgb24>(112, 112);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                image[x, y] = ((x + y) % 16 == 0) ? Color.Black : Color.White;
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsWebp(ms);
        return ms.ToArray();
    }

    private EnrollmentValidationService CreateIntegrationService()
    {
        var policy = new DefaultEnrollmentValidationPolicy(
            Options.Create(new EnrollmentValidationOptions
            {
                BlurThreshold = 0,
                MinimumBrightness = 0,
                MaximumBrightness = 1.0,
                MinimumContrast = 0,
            }),
            Options.Create(new InsightFaceOptions()));

        return new EnrollmentValidationService(
            policy,
            EnrollmentValidationTestFactory.CreateRegistry(),
            _faceAnalysis.Object,
            TimeProvider.System,
            NullLogger<EnrollmentValidationService>.Instance);
    }
}
