using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Constants;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.InsightFace;

/// <summary>InsightFace ONNX provider for student photo embedding generation.</summary>
public sealed class InsightFaceEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly InsightFaceEngine _engine;
    private readonly IMediaObjectReader _mediaReader;
    private readonly InsightFaceOptions _options;
    private readonly ILogger<InsightFaceEmbeddingGenerator> _logger;

    public InsightFaceEmbeddingGenerator(
        InsightFaceEngine engine,
        IMediaObjectReader mediaReader,
        IOptions<InsightFaceOptions> options,
        ILogger<InsightFaceEmbeddingGenerator> logger)
    {
        _engine = engine;
        _mediaReader = mediaReader;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => EmbeddingProviders.InsightFace;

    public string ModelName => _options.RecognitionModelFile;

    public string Version => _options.PipelineVersion;

    public async Task<EmbeddingGenerationResult> GenerateAsync(
        EmbeddingGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var imageBytes = await _mediaReader.ReadVariantAsync(request.PhotoKey, "original", cancellationToken);
        var embedding = _engine.GenerateSingleFaceEmbedding(imageBytes, cancellationToken);

        _logger.LogInformation(
            "InsightFace student embedding generated. StudentId={StudentId} TenantId={TenantId} Dimensions={Dimensions}",
            request.StudentId,
            request.TenantId,
            embedding.Length);

        return new EmbeddingGenerationResult(
            embedding,
            ModelName,
            Version,
            EmbeddingQuality.Good,
            _options.ExpectedEmbeddingDimension);
    }
}
