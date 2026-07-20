namespace Abhyanvaya.Application.Enrollment.Validation;

public enum ValidationOverallResult
{
    Passed = 0,
    Failed = 1,
}

public enum ValidationRuleOutcome
{
    Pass = 0,
    Fail = 1,
    Skipped = 2,
    NotApplicable = 3,
    Warning = 4,
    Information = 5,
}

public sealed record ValidationReport
{
    public required ValidationOverallResult OverallResult { get; init; }
    public float? OverallScore { get; init; }
    public required int FaceCount { get; init; }
    public float? FaceConfidence { get; init; }
    public float? BlurScore { get; init; }
    public PoseEstimate? Pose { get; init; }
    public float? Brightness { get; init; }
    public float? Contrast { get; init; }
    public int? SourceWidth { get; init; }
    public int? SourceHeight { get; init; }
    public int? FaceWidth { get; init; }
    public int? FaceHeight { get; init; }
    public float? FaceCoveragePercent { get; init; }
    public float? CompositeScore { get; init; }
    public float? DetectionConfidence { get; init; }
    public float? FaceSizeRatio { get; init; }
    public bool? EyesVisible { get; init; }
    public float? OcclusionCoverage { get; init; }
    public required IReadOnlyList<ValidationRuleResult> RuleResults { get; init; }
    public required IReadOnlyList<string> ValidationFailures { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public IReadOnlyList<string> InformationMessages { get; init; } = [];
    public ValidationSeveritySummary? SeveritySummary { get; init; }
    public EnrollmentValidationTelemetry? Telemetry { get; init; }

    /// <summary>True when a face crop is available for embedding generation.</summary>
    public bool EmbeddingEligible { get; init; }
}

public sealed record ValidationRuleResult
{
    public required string RuleId { get; init; }
    public required ValidationRuleOutcome Outcome { get; init; }
    public string? Message { get; init; }
    public double? MeasuredValue { get; init; }
    public double? ThresholdValue { get; init; }
}

public sealed record PoseEstimate
{
    public required float Yaw { get; init; }
    public required float Pitch { get; init; }
    public required float Roll { get; init; }
    public required float Deviation { get; init; }
}
