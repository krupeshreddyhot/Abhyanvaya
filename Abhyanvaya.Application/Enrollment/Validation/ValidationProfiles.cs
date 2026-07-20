namespace Abhyanvaya.Application.Enrollment.Validation;

/// <summary>How a failed rule affects enrollment eligibility.</summary>
public enum ValidationRuleEnforcement
{
    Hard = 0,
    Warning = 1,
    Information = 2,
}

public enum ValidationProfileKind
{
    Default = 0,
    Strict = 1,
    Mobile = 2,
    Kiosk = 3,
    Passport = 4,
    Exam = 5,
    /// <summary>Accept all downloadable photos for profile storage; quality rules become warnings and embedding may be skipped.</summary>
    PhotoCapture = 6,
}

public sealed record ValidationProfileDefinition
{
    public required ValidationProfileKind Kind { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyDictionary<string, bool> EnabledRules { get; init; }
    public required EnrollmentValidationThresholds ThresholdOverrides { get; init; }
    public IReadOnlyDictionary<string, ValidationRuleEnforcement>? SeverityOverrides { get; init; }
}

public static class ValidationProfiles
{
    public static ValidationProfileDefinition Default { get; } = Create(
        ValidationProfileKind.Default,
        "Default",
        enabledRules: null,
        thresholdOverrides: EnrollmentValidationThresholds.Default);

    public static ValidationProfileDefinition Strict { get; } = Create(
        ValidationProfileKind.Strict,
        "Strict",
        enabledRules: null,
        thresholdOverrides: EnrollmentValidationThresholds.Default with
        {
            BlurThreshold = 150.0,
            MinimumFaceCoverageRatio = 0.10,
            MinimumBrightness = 0.25,
            MaximumBrightness = 0.80,
            MinimumContrast = 0.12,
        });

    public static ValidationProfileDefinition Mobile { get; } = Create(
        ValidationProfileKind.Mobile,
        "Mobile",
        enabledRules: null,
        thresholdOverrides: EnrollmentValidationThresholds.Default with
        {
            BlurThreshold = 75.0,
            MinimumFaceCoverageRatio = 0.04,
        });

    public static ValidationProfileDefinition Kiosk { get; } = Create(
        ValidationProfileKind.Kiosk,
        "Kiosk",
        enabledRules: null,
        thresholdOverrides: EnrollmentValidationThresholds.Default with
        {
            MinimumFaceCoverageRatio = 0.08,
            MaximumAbsoluteYawDegrees = 20.0,
        });

    public static ValidationProfileDefinition Passport { get; } = Create(
        ValidationProfileKind.Passport,
        "Passport",
        enabledRules: null,
        thresholdOverrides: EnrollmentValidationThresholds.Default with
        {
            BlurThreshold = 120.0,
            MaximumAbsoluteYawDegrees = 15.0,
            MaximumAbsolutePitchDegrees = 15.0,
            MaximumAbsoluteRollDegrees = 15.0,
            MinimumFaceCoverageRatio = 0.15,
        });

    public static ValidationProfileDefinition Exam { get; } = Create(
        ValidationProfileKind.Exam,
        "Exam",
        enabledRules: null,
        thresholdOverrides: EnrollmentValidationThresholds.Default with
        {
            MinimumFaceCoverageRatio = 0.06,
            BlurThreshold = 90.0,
        });

    public static ValidationProfileDefinition PhotoCapture { get; } = Create(
        ValidationProfileKind.PhotoCapture,
        "PhotoCapture",
        enabledRules: null,
        thresholdOverrides: EnrollmentValidationThresholds.Default with
        {
            MinimumSourceWidth = 1,
            MinimumSourceHeight = 1,
            MinimumFaceWidth = 1,
            MinimumFaceHeight = 1,
            MinimumFaceCoverageRatio = 0,
            BlurThreshold = 0,
        },
        severityOverrides: BuildPhotoCaptureSeverityOverrides());

    public static IReadOnlyList<ValidationProfileDefinition> All { get; } =
    [
        Default,
        Strict,
        Mobile,
        Kiosk,
        Passport,
        Exam,
        PhotoCapture,
    ];

    public static ValidationProfileDefinition Resolve(ValidationProfileKind kind) =>
        All.First(p => p.Kind == kind);

    private static ValidationProfileDefinition Create(
        ValidationProfileKind kind,
        string name,
        IReadOnlyDictionary<string, bool>? enabledRules,
        EnrollmentValidationThresholds thresholdOverrides,
        IReadOnlyDictionary<string, ValidationRuleEnforcement>? severityOverrides = null) =>
        new()
        {
            Kind = kind,
            Name = name,
            EnabledRules = enabledRules ?? BuildAllEnabledRules(),
            ThresholdOverrides = thresholdOverrides,
            SeverityOverrides = severityOverrides,
        };

    private static IReadOnlyDictionary<string, ValidationRuleEnforcement> BuildPhotoCaptureSeverityOverrides() =>
        new Dictionary<string, ValidationRuleEnforcement>(StringComparer.Ordinal)
        {
            [EnrollmentValidationRuleIds.MinimumSourceResolution] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.MaximumSourceResolution] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.ExactlyOneFace] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.FaceConfidence] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.MinimumFaceCropResolution] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.FaceSizeCoverage] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.BlurScore] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.Pose] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.Brightness] = ValidationRuleEnforcement.Warning,
            [EnrollmentValidationRuleIds.Contrast] = ValidationRuleEnforcement.Warning,
        };

    private static IReadOnlyDictionary<string, bool> BuildAllEnabledRules() =>
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            [EnrollmentValidationRuleIds.ImageFormat] = true,
            [EnrollmentValidationRuleIds.CorruptImage] = true,
            [EnrollmentValidationRuleIds.MinimumSourceResolution] = true,
            [EnrollmentValidationRuleIds.MaximumSourceResolution] = true,
            [EnrollmentValidationRuleIds.ExactlyOneFace] = true,
            [EnrollmentValidationRuleIds.FaceConfidence] = true,
            [EnrollmentValidationRuleIds.MinimumFaceCropResolution] = true,
            [EnrollmentValidationRuleIds.FaceSizeCoverage] = true,
            [EnrollmentValidationRuleIds.BlurScore] = true,
            [EnrollmentValidationRuleIds.Pose] = true,
            [EnrollmentValidationRuleIds.Brightness] = true,
            [EnrollmentValidationRuleIds.Contrast] = true,
            [EnrollmentValidationRuleIds.Liveness] = false,
            [EnrollmentValidationRuleIds.MaskDetection] = false,
            [EnrollmentValidationRuleIds.EyeOpenness] = false,
            [EnrollmentValidationRuleIds.SpoofDetection] = false,
            [EnrollmentValidationRuleIds.Occlusion] = false,
            [EnrollmentValidationRuleIds.Sunglasses] = false,
            [EnrollmentValidationRuleIds.Smile] = false,
            [EnrollmentValidationRuleIds.Expression] = false,
        };
}
