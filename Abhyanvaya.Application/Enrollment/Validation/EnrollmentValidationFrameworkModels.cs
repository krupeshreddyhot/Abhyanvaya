namespace Abhyanvaya.Application.Enrollment.Validation;

public sealed record EnrollmentValidationRuleContext
{
    public required EnrollmentValidationRequest Request { get; init; }
    public required EnrollmentValidationPolicyDecision Policy { get; init; }
    public required EnrollmentValidationThresholds Thresholds { get; init; }
    public required IEnrollmentFaceAnalysisAccessor AnalysisAccessor { get; init; }
}

/// <summary>Lazy, single-pass image analysis seam used by validation rules.</summary>
public interface IEnrollmentFaceAnalysisAccessor
{
    EnrollmentImageIntegrityCheckerResult? FormatResult { get; }
    EnrollmentImageIntegrityCheckerResult ValidateFormat();
    Task<EnrollmentImageIntegrityCheckerResult?> GetDecodeResultAsync(CancellationToken cancellationToken);
    Task<EnrollmentFaceAnalysisResult?> GetAnalysisAsync(CancellationToken cancellationToken);
    Task<FaceQualityMetrics?> GetQualityMetricsAsync(CancellationToken cancellationToken);
    bool IsDetectionSkipped { get; }
    void MarkDetectionSkipped();
}

public sealed record EnrollmentImageIntegrityCheckerResult
{
    public required bool IsValid { get; init; }
    public string? FailureMessage { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public bool IsCorrupt { get; init; }
    public bool IsUnsupportedFormat { get; init; }
}

public sealed record FaceQualityMetrics
{
    public required float BlurScore { get; init; }
    public required float Brightness { get; init; }
    public required float Contrast { get; init; }
    public required PoseEstimate Pose { get; init; }
}

public sealed record EnrollmentFaceBoundingBox
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}

public sealed record EnrollmentValidationArtifact
{
    public required ValidationReport Report { get; init; }
    public byte[]? AlignedFaceImage { get; init; }
    public byte[]? SourcePhotoImage { get; init; }
    public EnrollmentFaceBoundingBox? BoundingBox { get; init; }
    public float[]? Landmarks { get; init; }
    public FaceQualityMetrics? FaceQualityMetrics { get; init; }
    public float? QualityScore { get; init; }
    public EnrollmentValidationTelemetry? Telemetry { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required Guid CorrelationId { get; init; }
    public ValidationDiagnosticImage? DiagnosticImages { get; init; }
}

public sealed record ValidationDiagnosticImage
{
    public byte[]? OriginalImage { get; init; }
    public byte[]? AlignedFace { get; init; }
    public byte[]? BoundingBoxOverlay { get; init; }
    public byte[]? LandmarksOverlay { get; init; }
    public byte[]? FaceCrop { get; init; }
}

public sealed record ValidationSeveritySummary
{
    public required int PassCount { get; init; }
    public required int FailCount { get; init; }
    public required int WarningCount { get; init; }
    public required int InformationCount { get; init; }
    public required int SkippedCount { get; init; }
    public required int NotApplicableCount { get; init; }
}
