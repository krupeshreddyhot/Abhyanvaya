using System.Diagnostics;
using System.Runtime.InteropServices;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Constants;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace Abhyanvaya.Infrastructure.Enrollment.Embedding;

/// <summary>InsightFace ArcFace implementation of <see cref="IEmbeddingEngine"/>.</summary>
public sealed class InsightFaceEmbeddingEngine : IEmbeddingEngine
{
    private readonly InsightFace.InsightFaceEngine _engine;
    private readonly InsightFace.InsightFaceOptions _options;

    public InsightFaceEmbeddingEngine(
        InsightFace.InsightFaceEngine engine,
        IOptions<InsightFace.InsightFaceOptions> options)
    {
        _engine = engine;
        _options = options.Value;
    }

    public string EngineName => EmbeddingProviders.InsightFace;

    public string ModelName => _options.RecognitionModelFile;

    public string ModelVersion => _options.PipelineVersion;

    public int ExpectedDimension => _options.ExpectedEmbeddingDimension;

    public string NormalizationMethod => "L2";

    public Task<EmbeddingEngineResult> GenerateFromAlignedFaceAsync(
        Stream alignedFaceStream,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var vector = _engine.GenerateEmbeddingFromAlignedFace(alignedFaceStream, cancellationToken);
        stopwatch.Stop();
        return Task.FromResult(new EmbeddingEngineResult(vector, stopwatch.ElapsedMilliseconds));
    }

    internal static string? ResolveFrameworkVersion() =>
        RuntimeInformation.FrameworkDescription;

    internal static string? ResolveOnnxVersion() =>
        typeof(InferenceSession).Assembly.GetName().Version?.ToString();
}
