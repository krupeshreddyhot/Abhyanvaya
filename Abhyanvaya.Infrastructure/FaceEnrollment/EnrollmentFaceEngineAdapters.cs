using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Application.FaceEnrollment;

namespace Abhyanvaya.Infrastructure.FaceEnrollment;

/// <summary>
/// Reuses <see cref="IEnrollmentFaceAnalysisService"/> for detection — no duplicate AI implementation.
/// </summary>
internal sealed class EnrollmentFaceAnalysisBridge : IFaceDetectionEngine, IFaceAlignmentEngine
{
    private readonly IEnrollmentFaceAnalysisService _analysisService;
    private EnrollmentFaceAnalysisResult? _lastResult;

    public EnrollmentFaceAnalysisBridge(IEnrollmentFaceAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    public async Task<FaceDetectionResult> DetectAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        _lastResult = await _analysisService.AnalyzeAsync(imageBytes, cancellationToken);
        var topConfidence = _lastResult.Faces.Count == 0
            ? 0f
            : _lastResult.Faces.Max(f => f.DetectionScore);

        return new FaceDetectionResult
        {
            FaceCount = _lastResult.Faces.Count,
            TopConfidence = topConfidence,
            ImageWidth = _lastResult.ImageWidth,
            ImageHeight = _lastResult.ImageHeight,
        };
    }

    public Task<FaceAlignmentResult> AlignAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (_lastResult?.AlignedFaceWebpBytes is not { Length: > 0 })
        {
            return Task.FromResult(new FaceAlignmentResult
            {
                Success = false,
                FailureReason = "Aligned face bytes unavailable after detection.",
            });
        }

        return Task.FromResult(new FaceAlignmentResult
        {
            Success = true,
            AlignedFaceBytes = _lastResult.AlignedFaceWebpBytes,
            ContentType = "image/webp",
        });
    }
}
