using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal static class ValidationDiagnosticImageBuilder
{
    internal static async Task<ValidationDiagnosticImage?> BuildOptionalAsync(
        EnrollmentFaceAnalysisAccessor accessor,
        CancellationToken cancellationToken)
    {
        var originalBytes = await accessor.GetImageBytesAsync(cancellationToken);
        if (originalBytes is not { Length: > 0 })
        {
            return null;
        }

        var analysis = accessor.GetCachedAnalysis();
        if (analysis?.Faces.Count != 1)
        {
            return new ValidationDiagnosticImage
            {
                OriginalImage = originalBytes,
            };
        }

        var face = analysis.Faces[0];

        return new ValidationDiagnosticImage
        {
            OriginalImage = originalBytes,
            AlignedFace = analysis.AlignedFaceWebpBytes,
            FaceCrop = analysis.AlignedFaceWebpBytes,
            BoundingBoxOverlay = null,
            LandmarksOverlay = null,
        };
    }

    internal static EnrollmentValidationArtifact BuildArtifact(
        ValidationReport report,
        EnrollmentValidationRequest request,
        EnrollmentFaceAnalysisAccessor accessor,
        DateTimeOffset timestampUtc,
        EnrollmentValidationTelemetry? telemetry,
        ValidationDiagnosticImage? diagnosticImages)
    {
        var analysis = accessor.GetCachedAnalysis();
        EnrollmentDetectedFace? face = analysis?.Faces.Count == 1 ? analysis.Faces[0] : null;
        var metrics = accessor.GetCachedQualityMetrics();
        var sourcePhoto = diagnosticImages?.OriginalImage;
        var alignedFace = report.EmbeddingEligible ? analysis?.AlignedFaceWebpBytes : null;

        return new EnrollmentValidationArtifact
        {
            Report = report,
            AlignedFaceImage = alignedFace,
            SourcePhotoImage = sourcePhoto,
            BoundingBox = face is null
                ? null
                : new EnrollmentFaceBoundingBox
                {
                    X = face.BoundingBoxX,
                    Y = face.BoundingBoxY,
                    Width = face.BoundingBoxWidth,
                    Height = face.BoundingBoxHeight,
                },
            Landmarks = face?.Landmarks,
            FaceQualityMetrics = metrics,
            QualityScore = report.CompositeScore,
            Telemetry = telemetry,
            TimestampUtc = timestampUtc,
            CorrelationId = request.ExecutionContext.CorrelationId,
            DiagnosticImages = diagnosticImages,
        };
    }
}
