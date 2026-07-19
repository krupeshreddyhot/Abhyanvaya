using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Infrastructure.Enrollment.Validation.Rules;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal sealed class EnrollmentFaceAnalysisAccessor : IEnrollmentFaceAnalysisAccessor
{
    private readonly IEnrollmentFaceAnalysisService _faceAnalysisService;
    private readonly EnrollmentValidationRequest _request;
    private readonly EnrollmentValidationThresholds _thresholds;

    private EnrollmentImageIntegrityCheckerResult? _formatResult;
    private EnrollmentImageIntegrityCheckerResult? _decodeResult;
    private EnrollmentFaceAnalysisResult? _analysis;
    private FaceQualityMetrics? _qualityMetrics;
    private byte[]? _imageBytes;
    private bool _detectionSkipped;

    public EnrollmentFaceAnalysisAccessor(
        EnrollmentValidationRequest request,
        EnrollmentValidationThresholds thresholds,
        IEnrollmentFaceAnalysisService faceAnalysisService)
    {
        _request = request;
        _thresholds = thresholds;
        _faceAnalysisService = faceAnalysisService;
    }

    public EnrollmentImageIntegrityCheckerResult? FormatResult => _formatResult;

    public bool IsDetectionSkipped => _detectionSkipped;

    public void MarkDetectionSkipped() => _detectionSkipped = true;

    public EnrollmentImageIntegrityCheckerResult ValidateFormat()
    {
        _formatResult ??= Map(EnrollmentImageIntegrityChecker.ValidateFormat(
            _request.ImageMetadata.FileName,
            _request.ImageMetadata.ByteSize,
            _thresholds.MaxImageBytes));

        if (!_formatResult.IsValid)
        {
            _detectionSkipped = true;
        }

        return _formatResult;
    }

    public async Task<EnrollmentImageIntegrityCheckerResult?> GetDecodeResultAsync(CancellationToken cancellationToken)
    {
        if (_decodeResult is not null)
        {
            return _decodeResult;
        }

        if (_detectionSkipped || _formatResult is { IsValid: false })
        {
            return null;
        }

        _decodeResult = Map(await EnrollmentImageIntegrityChecker.ValidateDecodeAsync(
            _request.ImageStream,
            cancellationToken));

        if (_decodeResult is { IsValid: false })
        {
            _detectionSkipped = true;
        }

        return _decodeResult;
    }

    public async Task<EnrollmentFaceAnalysisResult?> GetAnalysisAsync(CancellationToken cancellationToken)
    {
        if (_analysis is not null)
        {
            return _analysis;
        }

        if (_detectionSkipped)
        {
            return null;
        }

        var decode = await GetDecodeResultAsync(cancellationToken);
        if (decode is not { IsValid: true })
        {
            _detectionSkipped = true;
            return null;
        }

        if (_imageBytes is null)
        {
            _imageBytes = await EnrollmentImageIntegrityChecker.ReadAllBytesAsync(
                _request.ImageStream,
                cancellationToken);
        }

        _analysis = await _faceAnalysisService.AnalyzeAsync(_imageBytes, cancellationToken);
        return _analysis;
    }

    public async Task<FaceQualityMetrics?> GetQualityMetricsAsync(CancellationToken cancellationToken)
    {
        if (_qualityMetrics is not null)
        {
            return _qualityMetrics;
        }

        var analysis = await GetAnalysisAsync(cancellationToken);
        if (analysis?.Faces.Count != 1 || analysis.AlignedFaceWebpBytes is null)
        {
            return null;
        }

        var metrics = EnrollmentFaceQualityAnalyzer.AnalyzeFromWebpBytes(
            analysis.AlignedFaceWebpBytes,
            analysis.Faces[0].Landmarks);

        _qualityMetrics = new FaceQualityMetrics
        {
            BlurScore = metrics.BlurScore,
            Brightness = metrics.Brightness,
            Contrast = metrics.Contrast,
            Pose = metrics.Pose,
        };

        return _qualityMetrics;
    }

    internal EnrollmentFaceAnalysisResult? GetCachedAnalysis() => _analysis;

    internal FaceQualityMetrics? GetCachedQualityMetrics() => _qualityMetrics;

    internal int? SourceWidth => _analysis?.ImageWidth ?? _decodeResult?.Width;

    internal int? SourceHeight => _analysis?.ImageHeight ?? _decodeResult?.Height;

    public async Task<byte[]?> GetImageBytesAsync(CancellationToken cancellationToken)
    {
        if (_imageBytes is not null)
        {
            return _imageBytes;
        }

        if (_detectionSkipped)
        {
            return null;
        }

        _imageBytes = await EnrollmentImageIntegrityChecker.ReadAllBytesAsync(
            _request.ImageStream,
            cancellationToken);
        return _imageBytes;
    }

    private static EnrollmentImageIntegrityCheckerResult Map(
        EnrollmentImageIntegrityChecker.IntegrityCheckResult result) =>
        new()
        {
            IsValid = result.IsValid,
            FailureMessage = result.FailureMessage,
            Width = result.Width,
            Height = result.Height,
            IsCorrupt = result.IsCorrupt,
            IsUnsupportedFormat = result.IsUnsupportedFormat,
        };
}
