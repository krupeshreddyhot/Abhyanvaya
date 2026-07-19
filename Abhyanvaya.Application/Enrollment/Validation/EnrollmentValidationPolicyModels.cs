namespace Abhyanvaya.Application.Enrollment.Validation;

public sealed record EnrollmentValidationThresholds
{
    public required long MaxImageBytes { get; init; }
    public required int MinimumSourceWidth { get; init; }
    public required int MinimumSourceHeight { get; init; }
    public required int MaximumSourceWidth { get; init; }
    public required int MaximumSourceHeight { get; init; }
    public required int MinimumFaceWidth { get; init; }
    public required int MinimumFaceHeight { get; init; }
    public required double MinimumFaceCoverageRatio { get; init; }
    public required double BlurThreshold { get; init; }
    public required double BlurNormalizationReference { get; init; }
    public required double MaximumAbsoluteYawDegrees { get; init; }
    public required double MaximumAbsolutePitchDegrees { get; init; }
    public required double MaximumAbsoluteRollDegrees { get; init; }
    public required double MaximumPoseDeviationDegrees { get; init; }
    public required double MinimumBrightness { get; init; }
    public required double MaximumBrightness { get; init; }
    public required double MinimumContrast { get; init; }
    public required float DetectionConfidenceThreshold { get; init; }
    public required double CompositeWeightDetection { get; init; }
    public required double CompositeWeightFaceArea { get; init; }
    public required double CompositeWeightSharpness { get; init; }
    public required double CompositeWeightPose { get; init; }

    public static EnrollmentValidationThresholds Default { get; } = new()
    {
        MaxImageBytes = 15 * 1024 * 1024,
        MinimumSourceWidth = 640,
        MinimumSourceHeight = 480,
        MaximumSourceWidth = 8192,
        MaximumSourceHeight = 8192,
        MinimumFaceWidth = 112,
        MinimumFaceHeight = 112,
        MinimumFaceCoverageRatio = 0.05,
        BlurThreshold = 100.0,
        BlurNormalizationReference = 500.0,
        MaximumAbsoluteYawDegrees = 25.0,
        MaximumAbsolutePitchDegrees = 25.0,
        MaximumAbsoluteRollDegrees = 25.0,
        MaximumPoseDeviationDegrees = 25.0,
        MinimumBrightness = 0.20,
        MaximumBrightness = 0.85,
        MinimumContrast = 0.08,
        DetectionConfidenceThreshold = 0.50f,
        CompositeWeightDetection = 0.40,
        CompositeWeightFaceArea = 0.20,
        CompositeWeightSharpness = 0.25,
        CompositeWeightPose = 0.15,
    };
}

public sealed record EnrollmentValidationPolicyRequest
{
    public required int TenantId { get; init; }
    public int? CollegeId { get; init; }
    public int? ProgramId { get; init; }
    public string? ExamType { get; init; }
    public string? AdmissionType { get; init; }
    public ValidationProfileKind? RequestedProfile { get; init; }
}

public sealed record EnrollmentValidationPolicyDecision
{
    public required ValidationProfileDefinition Profile { get; init; }
    public required EnrollmentValidationThresholds Thresholds { get; init; }
    public IReadOnlyDictionary<string, bool>? RuleEnableOverrides { get; init; }
    public IReadOnlyDictionary<string, ValidationRuleEnforcement>? SeverityOverrides { get; init; }
}
