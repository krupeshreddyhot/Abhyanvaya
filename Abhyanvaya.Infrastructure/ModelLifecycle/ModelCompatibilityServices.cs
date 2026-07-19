using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.ModelLifecycle;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ModelLifecycle;

public sealed class ModelLifecycleOptions
{
    public const string SectionName = "ModelLifecycle";

    public int SupportedPipelineVersion { get; set; } = 1;

    public string DefaultEmbeddingVersion { get; set; } = "insightface-1.0";

    public string DefaultRecognitionVersion { get; set; } = "insightface-1.0";

    public decimal DriftAccuracyThresholdPercent { get; set; } = 5m;

    public decimal DriftUnknownThresholdPercent { get; set; } = 10m;
}

public sealed class ModelCompatibilityService : IModelCompatibilityService
{
    private readonly IEmbeddingCompatibilityService _embeddingCompatibility;
    private readonly ModelLifecycleOptions _options;

    public ModelCompatibilityService(
        IEmbeddingCompatibilityService embeddingCompatibility,
        IOptions<ModelLifecycleOptions> options)
    {
        _embeddingCompatibility = embeddingCompatibility;
        _options = options.Value;
    }

    public ModelCompatibilityResult Validate(AIModelDescriptor model, int pipelineVersion) =>
        _embeddingCompatibility.CheckCompatibility(
            model.EmbeddingVersion,
            model.RecognitionVersion,
            pipelineVersion);
}

public sealed class EmbeddingCompatibilityService : IEmbeddingCompatibilityService
{
    private readonly ModelLifecycleOptions _options;

    public EmbeddingCompatibilityService(IOptions<ModelLifecycleOptions> options)
    {
        _options = options.Value;
    }

    public ModelCompatibilityResult CheckCompatibility(
        string embeddingVersion,
        string recognitionVersion,
        int pipelineVersion)
    {
        var issues = new List<string>();

        if (pipelineVersion > _options.SupportedPipelineVersion)
        {
            issues.Add($"Pipeline version {pipelineVersion} exceeds supported {_options.SupportedPipelineVersion}.");
        }

        if (string.IsNullOrWhiteSpace(embeddingVersion))
        {
            issues.Add("Embedding version is required.");
        }

        if (string.IsNullOrWhiteSpace(recognitionVersion))
        {
            issues.Add("Recognition version is required.");
        }

        var migrationRequired = !embeddingVersion.StartsWith(_options.DefaultEmbeddingVersion, StringComparison.OrdinalIgnoreCase)
            || !recognitionVersion.StartsWith(_options.DefaultRecognitionVersion, StringComparison.OrdinalIgnoreCase);

        return new ModelCompatibilityResult
        {
            IsCompatible = issues.Count == 0,
            MigrationRequired = migrationRequired,
            BackwardCompatible = pipelineVersion <= _options.SupportedPipelineVersion,
            Issues = issues.Count > 0 ? issues : null,
        };
    }
}
