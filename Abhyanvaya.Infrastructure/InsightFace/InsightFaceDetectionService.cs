using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Domain.Constants;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.InsightFace;

/// <summary>InsightFace ONNX face detection service (returns detections only).</summary>
public sealed class InsightFaceDetectionService : IFaceDetectionService
{
    private readonly InsightFaceEngine _engine;
    private readonly InsightFaceOptions _options;

    public InsightFaceDetectionService(InsightFaceEngine engine, IOptions<InsightFaceOptions> options)
    {
        _engine = engine;
        _options = options.Value;
    }

    public string ProviderName => EmbeddingProviders.InsightFace;

    public string ModelName => _options.RecognitionModelFile;

    public string Version => _options.PipelineVersion;

    public Task<FaceDetectionResponse> DetectAsync(
        FaceDetectionRequest request,
        CancellationToken cancellationToken = default) =>
        _engine.DetectAsync(request, cancellationToken);
}
