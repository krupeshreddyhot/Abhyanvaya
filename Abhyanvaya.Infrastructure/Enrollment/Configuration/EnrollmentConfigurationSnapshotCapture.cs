using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Application.Enrollment.Configuration;
using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;
using Abhyanvaya.Infrastructure.Embedding;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;
using Abhyanvaya.Infrastructure.Enrollment.PhotoProviders;
using Abhyanvaya.Infrastructure.Enrollment.Pipeline;
using Abhyanvaya.Infrastructure.InsightFace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Configuration;

public sealed class EnrollmentConfigurationSnapshotCapture : IEnrollmentConfigurationSnapshotCapture
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly IConfiguration _configuration;
    private readonly ExamBranchPhotoProviderOptions _examBranchOptions;
    private readonly InsightFaceOptions _insightFaceOptions;

    public EnrollmentConfigurationSnapshotCapture(
        IConfiguration configuration,
        IOptions<ExamBranchPhotoProviderOptions> examBranchOptions,
        IOptions<InsightFaceOptions> insightFaceOptions)
    {
        _configuration = configuration;
        _examBranchOptions = examBranchOptions.Value;
        _insightFaceOptions = insightFaceOptions.Value;
    }

    public Task<ConfigurationSnapshotCaptureResult> CaptureAsync(
        EnrollmentBatchRequest request,
        int pipelineVersion,
        PipelineManifest manifest,
        string photoProviderName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(photoProviderName))
            {
                return Task.FromResult(ConfigurationSnapshotCaptureResult.Fail(
                    "Photo provider name is required for configuration snapshot capture."));
            }

            if (string.IsNullOrWhiteSpace(_examBranchOptions.BaseUrlTemplate)
                && string.Equals(photoProviderName, Domain.Constants.StudentPhotoProviders.ExamBranch, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(ConfigurationSnapshotCaptureResult.Fail(
                    "ExamBranch photo provider BaseUrlTemplate is not configured."));
            }

            var capturedUtc = DateTime.UtcNow;
            var manifestHash = ConfigurationPipelineManifestProvider.ComputeManifestHash(manifest);
            var totalStudentsPlaceholder = 0;
            var retryBudget = BuildRetryBudget(totalStudentsPlaceholder);

            var snapshotWithoutHash = new ConfigurationSnapshot
            {
                SchemaVersion = 1,
                CapturedUtc = capturedUtc,
                SnapshotHash = string.Empty,
                EmbeddingProvider = _configuration["Embedding:DefaultProvider"] ?? "InsightFace",
                EmbeddingModel = Path.GetFileNameWithoutExtension(_insightFaceOptions.RecognitionModelFile),
                EmbeddingVersion = "insightface-r50-v1.0",
                EngineProvider = "InsightFace",
                NormalizationMethod = "L2",
                AiModelVersion = _insightFaceOptions.PipelineVersion,
                ModelChecksum = null,
                Thresholds = new ThresholdSnapshot
                {
                    RecognitionMatchDistanceThreshold = ReadFloat("Recognition:MatchDistanceThreshold", 0.45f),
                    RecognitionLowConfidenceDistanceThreshold = ReadFloat("Recognition:LowConfidenceDistanceThreshold", 0.55f),
                    MinimumCompositeQualityScore = null,
                    DetectionThreshold = _insightFaceOptions.DetectionThreshold,
                    NmsThreshold = _insightFaceOptions.NmsThreshold,
                },
                PhotoProvider = photoProviderName,
                PhotoProviderSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["baseUrlTemplate"] = _examBranchOptions.BaseUrlTemplate,
                    ["timeoutSeconds"] = Math.Max(1, _examBranchOptions.TimeoutSeconds).ToString(),
                },
                ValidationRules = BuildValidationRules(),
                StorageProvider = _configuration["Branding:Provider"] ?? "local",
                StorageProviderSettings = BuildStorageSettings(),
                PipelineVersion = pipelineVersion,
                PipelineManifest = new PipelineManifestReference
                {
                    PipelineName = manifest.PipelineName,
                    PipelineVersion = manifest.PipelineVersion,
                    ManifestSchemaVersion = manifest.SchemaVersion,
                    ManifestHash = manifestHash,
                },
                RetryPolicy = BuildRetryPolicy(retryBudget),
                FeatureFlags = BuildFeatureFlags(manifest),
                FuturePromptVersion = null,
            };

            var serialized = JsonSerializer.Serialize(snapshotWithoutHash, SerializerOptions);
            var hash = ComputeSnapshotHash(serialized);
            var snapshot = snapshotWithoutHash with { SnapshotHash = hash };
            var finalJson = JsonSerializer.Serialize(snapshot, SerializerOptions);

            return Task.FromResult(ConfigurationSnapshotCaptureResult.Ok(snapshot, finalJson));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ConfigurationSnapshotCaptureResult.Fail(
                $"Configuration snapshot capture failed: {ex.Message}"));
        }
    }

    private float ReadFloat(string key, float fallback)
    {
        var value = _configuration[key];
        return float.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static ValidationRulesSnapshot BuildValidationRules() =>
        new()
        {
            RequireExactlyOneFace = true,
            MinimumSourceWidth = 640,
            MinimumSourceHeight = 480,
            MinimumFaceWidth = 112,
            MinimumFaceHeight = 112,
            BlurMethod = "VarianceOfLaplacian",
            BlurThreshold = 100.0,
            MaximumAbsoluteYawDegrees = 25.0,
            MaximumAbsolutePitchDegrees = 25.0,
            MaximumAbsoluteRollDegrees = 25.0,
            CompositeQualityIsAdvisory = true,
            CompositeQualityWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["detection"] = 0.30,
                ["faceArea"] = 0.20,
                ["sharpness"] = 0.30,
                ["pose"] = 0.20,
            },
        };

    private static IReadOnlyDictionary<string, string> BuildStorageSettings() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyLayoutVersion"] = "students-v1",
        };

    private static RetryPolicySnapshot BuildRetryPolicy(RetryBudgetSnapshot batchBudget) =>
        new()
        {
            PolicySetVersion = "v1",
            StagePolicyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Download"] = "TransientEngine",
                ["Validation"] = "ValidationPermanent",
                ["Storage"] = "TransientEngine",
                ["Embedding"] = "TransientEngine",
                ["Finalize"] = "TransientEngine",
            },
            MaxAutomaticRetries = 3,
            MaximumAutomaticAttempts = 4,
            RetryWindow = TimeSpan.FromHours(24),
            BackoffStrategy = "ExponentialFullJitter",
            BaseDelay = TimeSpan.FromSeconds(30),
            MaxDelay = TimeSpan.FromMinutes(30),
            MaximumConsecutiveImmediateRetries = 1,
            BatchBudget = batchBudget,
            StageBudgetCosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Download"] = 1,
                ["Validation"] = 2,
                ["Storage"] = 2,
                ["Embedding"] = 3,
                ["Finalize"] = 3,
            },
            AutomaticRetrySafetyFloor =
            [
                "PhotoNotFound",
                "AccessDenied",
                "NoFaceDetected",
                "MultipleFacesDetected",
            ],
            ScheduledRetryHonorsRetryAfter = true,
            LowLevelPhotoImport = new StageInvocationRetrySnapshot
            {
                MaxRetriesWithinAttempt = 3,
                BackoffSeconds = [2, 4, 8],
                RetryableConditions =
                [
                    "HttpRequestException",
                    "TaskCanceledException",
                    "Http5xx",
                    "Http429",
                ],
            },
        };

    private static RetryBudgetSnapshot BuildRetryBudget(int totalStudents)
    {
        var capacity = Math.Max(25, (int)Math.Ceiling(totalStudents * 0.25));
        var refill = Math.Max(1, (int)Math.Ceiling(totalStudents * 0.01));
        var ceiling = Math.Max(50, (int)Math.Ceiling(totalStudents * 0.50));
        return new RetryBudgetSnapshot
        {
            CapacityTokens = capacity,
            RefillTokens = refill,
            RefillInterval = TimeSpan.FromHours(1),
            LifetimeSpendCeilingTokens = ceiling,
        };
    }

    private static IReadOnlyDictionary<string, bool> BuildFeatureFlags(PipelineManifest manifest)
    {
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["Liveness"] = false,
            ["Mask"] = false,
            ["Occlusion"] = false,
            ["Spoof"] = false,
            ["FaceQualityRanking"] = false,
            ["FaceNormalization"] = false,
            ["DuplicateDetection"] = false,
        };

        foreach (var stage in manifest.Stages.Where(s => s.Kind == StageKind.Optional))
        {
            flags[stage.Stage.ToString()] = stage.Enabled;
        }

        return flags;
    }

    private static string ComputeSnapshotHash(string canonicalJsonWithoutHashField)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJsonWithoutHashField));
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
