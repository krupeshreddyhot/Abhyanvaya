namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

using Abhyanvaya.Application.Enrollment.Validation;

public sealed class EnrollmentValidationOptions
{
    public const string SectionName = "EnrollmentValidation";

    public ValidationProfileKind DefaultProfile { get; set; } = ValidationProfileKind.PhotoCapture;

    public long MaxImageBytes { get; set; } = 15 * 1024 * 1024;

    public int MinimumSourceWidth { get; set; } = 1;
    public int MinimumSourceHeight { get; set; } = 1;
    public int MaximumSourceWidth { get; set; } = 8192;
    public int MaximumSourceHeight { get; set; } = 8192;

    public int MinimumFaceWidth { get; set; } = 32;
    public int MinimumFaceHeight { get; set; } = 32;
    public double MinimumFaceCoverageRatio { get; set; } = 0.05;

    public double BlurThreshold { get; set; } = 100.0;
    public double BlurNormalizationReference { get; set; } = 500.0;

    public double MaximumAbsoluteYawDegrees { get; set; } = 25.0;
    public double MaximumAbsolutePitchDegrees { get; set; } = 25.0;
    public double MaximumAbsoluteRollDegrees { get; set; } = 25.0;
    public double MaximumPoseDeviationDegrees { get; set; } = 25.0;

    public double MinimumBrightness { get; set; } = 0.20;
    public double MaximumBrightness { get; set; } = 0.85;
    public double MinimumContrast { get; set; } = 0.08;

    public double CompositeWeightDetection { get; set; } = 0.40;
    public double CompositeWeightFaceArea { get; set; } = 0.20;
    public double CompositeWeightSharpness { get; set; } = 0.25;
    public double CompositeWeightPose { get; set; } = 0.15;
}
