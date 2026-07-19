using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Enrollment.Validation;

public sealed record EnrollmentValidationRequest
{
    public required int StudentId { get; init; }
    public required Guid BatchId { get; init; }
    public required EnrollmentValidationExecutionContext ExecutionContext { get; init; }
    public required Stream ImageStream { get; init; }
    public required EnrollmentImageMetadata ImageMetadata { get; init; }

    /// <summary>Optional validation profile override (AI20.PHASE2.1.4A). Defaults to institution policy.</summary>
    public ValidationProfileKind? ValidationProfile { get; init; }
}

public sealed record EnrollmentValidationExecutionContext
{
    public required int TenantId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid ExecutionTraceId { get; init; }
    public required int PipelineVersion { get; init; }
}

public sealed record EnrollmentImageMetadata
{
    public required string FileName { get; init; }
    public string? ContentType { get; init; }
    public required long ByteSize { get; init; }
}

public sealed record EnrollmentValidationResult
{
    public required bool ValidationPassed { get; init; }
    public required ValidationReport Report { get; init; }
    public FailureCategory? FailureCategory { get; init; }
    public string? FailureReason { get; init; }
    public string? DiagnosticCode { get; init; }
    public EnrollmentValidationTelemetry? Telemetry { get; init; }
    public required TimeSpan Duration { get; init; }

    /// <summary>WebP-encoded aligned single-face crop for downstream storage/embedding. Null on failure.</summary>
    public byte[]? AlignedFaceBytes { get; init; }

    /// <summary>Structured validation artifact for downstream pipeline phases (AI20.PHASE2.1.4A).</summary>
    public EnrollmentValidationArtifact? Artifact { get; init; }

    /// <summary>Backward-compatible alias for baseline contract consumers.</summary>
    public bool IsValid => ValidationPassed;

    public string? Reason => FailureReason;

    public float? QualityScore => Report.CompositeScore;

    public int? SourceWidth => Report.SourceWidth;

    public int? SourceHeight => Report.SourceHeight;
}

public sealed record EnrollmentValidationTelemetry
{
    public required long ElapsedMilliseconds { get; init; }
    public required string Engine { get; init; }
    public required string Model { get; init; }
    public long? ImageSizeBytes { get; init; }
    public required int RulesExecuted { get; init; }
    public required int RulesPassed { get; init; }
    public required int RulesFailed { get; init; }
    public required int RulesSkipped { get; init; }
    public Guid? CorrelationId { get; init; }
    public Guid? ExecutionTraceId { get; init; }
    public int? PipelineVersion { get; init; }
}

public sealed record EnrollmentFaceAnalysisResult
{
    public required int ImageWidth { get; init; }
    public required int ImageHeight { get; init; }
    public required IReadOnlyList<EnrollmentDetectedFace> Faces { get; init; }
    public byte[]? AlignedFaceWebpBytes { get; init; }
}

public sealed record EnrollmentDetectedFace
{
    public required float DetectionScore { get; init; }
    public required int BoundingBoxX { get; init; }
    public required int BoundingBoxY { get; init; }
    public required int BoundingBoxWidth { get; init; }
    public required int BoundingBoxHeight { get; init; }
    public required float[] Landmarks { get; init; }
}
