using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Constants;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.InsightFace;

public sealed class InsightFaceEnrollmentFaceAnalysisService : IEnrollmentFaceAnalysisService
{
    private readonly InsightFaceEngine _engine;
    private readonly InsightFaceOptions _options;

    public InsightFaceEnrollmentFaceAnalysisService(
        InsightFaceEngine engine,
        IOptions<InsightFaceOptions> options)
    {
        _engine = engine;
        _options = options.Value;
    }

    public string ProviderName => EmbeddingProviders.InsightFace;

    public string ModelName => _options.DetectionModelFile;

    public string PipelineVersion => _options.PipelineVersion;

    public async Task<EnrollmentFaceAnalysisResult> AnalyzeAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        var engineResult = await _engine.AnalyzeForEnrollmentValidationAsync(imageBytes, cancellationToken);
        var faces = engineResult.Faces
            .Select(f => new EnrollmentDetectedFace
            {
                DetectionScore = f.DetectionScore,
                BoundingBoxX = f.BoundingBoxX,
                BoundingBoxY = f.BoundingBoxY,
                BoundingBoxWidth = f.BoundingBoxWidth,
                BoundingBoxHeight = f.BoundingBoxHeight,
                Landmarks = f.Landmarks,
            })
            .ToList();

        return new EnrollmentFaceAnalysisResult
        {
            ImageWidth = engineResult.ImageWidth,
            ImageHeight = engineResult.ImageHeight,
            Faces = faces,
            AlignedFaceWebpBytes = engineResult.AlignedFaceWebpBytes,
        };
    }
}
