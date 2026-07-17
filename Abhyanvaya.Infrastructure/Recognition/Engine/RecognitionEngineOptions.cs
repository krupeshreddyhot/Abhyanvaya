using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

public sealed class RecognitionEngineOptions
{
    public const string SectionName = "RecognitionEngine";

    public int DefaultTopK { get; set; } = 10;

    public float MatchDistanceThreshold { get; set; } = 0.45f;

    public float LowConfidenceDistanceThreshold { get; set; } = 0.55f;

    public float MinimumConfidence { get; set; } = 55f;

    public float UnknownThreshold { get; set; } = 45f;

    public float TieThreshold { get; set; } = 0.02f;

    public int MaximumCandidates { get; set; } = 10000;

    public bool AutoAccept { get; set; } = true;

    public bool ManualReviewEnabled { get; set; } = true;

    public int PipelineVersion { get; set; } = 1;
}

public sealed class ConfigurableRecognitionPolicy : IRecognitionPolicy
{
    private readonly RecognitionEngineOptions _options;

    public ConfigurableRecognitionPolicy(IOptions<RecognitionEngineOptions> options)
    {
        _options = options.Value;
    }

    public float MinimumConfidence => _options.MinimumConfidence;

    public float UnknownThreshold => _options.UnknownThreshold;

    public float TieThreshold => _options.TieThreshold;

    public int MaximumCandidates => _options.MaximumCandidates;

    public bool AutoAccept => _options.AutoAccept;

    public bool ManualReviewEnabled => _options.ManualReviewEnabled;

    public float MatchDistanceThreshold => _options.MatchDistanceThreshold;

    public float LowConfidenceDistanceThreshold => _options.LowConfidenceDistanceThreshold;
}
