using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Infrastructure.InsightFace;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal static class EnrollmentValidationThresholdMapper
{
    public static EnrollmentValidationThresholds FromOptions(
        EnrollmentValidationOptions options,
        InsightFaceOptions insightFaceOptions) =>
        new()
        {
            MaxImageBytes = options.MaxImageBytes,
            MinimumSourceWidth = options.MinimumSourceWidth,
            MinimumSourceHeight = options.MinimumSourceHeight,
            MaximumSourceWidth = options.MaximumSourceWidth,
            MaximumSourceHeight = options.MaximumSourceHeight,
            MinimumFaceWidth = options.MinimumFaceWidth,
            MinimumFaceHeight = options.MinimumFaceHeight,
            MinimumFaceCoverageRatio = options.MinimumFaceCoverageRatio,
            BlurThreshold = options.BlurThreshold,
            BlurNormalizationReference = options.BlurNormalizationReference,
            MaximumAbsoluteYawDegrees = options.MaximumAbsoluteYawDegrees,
            MaximumAbsolutePitchDegrees = options.MaximumAbsolutePitchDegrees,
            MaximumAbsoluteRollDegrees = options.MaximumAbsoluteRollDegrees,
            MaximumPoseDeviationDegrees = options.MaximumPoseDeviationDegrees,
            MinimumBrightness = options.MinimumBrightness,
            MaximumBrightness = options.MaximumBrightness,
            MinimumContrast = options.MinimumContrast,
            DetectionConfidenceThreshold = insightFaceOptions.DetectionThreshold,
            CompositeWeightDetection = options.CompositeWeightDetection,
            CompositeWeightFaceArea = options.CompositeWeightFaceArea,
            CompositeWeightSharpness = options.CompositeWeightSharpness,
            CompositeWeightPose = options.CompositeWeightPose,
        };

    public static EnrollmentValidationThresholds ApplyProfile(
        EnrollmentValidationThresholds baseline,
        ValidationProfileDefinition profile)
    {
        if (profile.Kind == ValidationProfileKind.Default)
        {
            return baseline;
        }

        var template = profile.ThresholdOverrides;
        var defaults = EnrollmentValidationThresholds.Default;

        return baseline with
        {
            BlurThreshold = template.BlurThreshold != defaults.BlurThreshold ? template.BlurThreshold : baseline.BlurThreshold,
            MinimumFaceCoverageRatio = template.MinimumFaceCoverageRatio != defaults.MinimumFaceCoverageRatio
                ? template.MinimumFaceCoverageRatio
                : baseline.MinimumFaceCoverageRatio,
            MinimumBrightness = template.MinimumBrightness != defaults.MinimumBrightness
                ? template.MinimumBrightness
                : baseline.MinimumBrightness,
            MaximumBrightness = template.MaximumBrightness != defaults.MaximumBrightness
                ? template.MaximumBrightness
                : baseline.MaximumBrightness,
            MinimumContrast = template.MinimumContrast != defaults.MinimumContrast
                ? template.MinimumContrast
                : baseline.MinimumContrast,
            MaximumAbsoluteYawDegrees = template.MaximumAbsoluteYawDegrees != defaults.MaximumAbsoluteYawDegrees
                ? template.MaximumAbsoluteYawDegrees
                : baseline.MaximumAbsoluteYawDegrees,
            MaximumAbsolutePitchDegrees = template.MaximumAbsolutePitchDegrees != defaults.MaximumAbsolutePitchDegrees
                ? template.MaximumAbsolutePitchDegrees
                : baseline.MaximumAbsolutePitchDegrees,
            MaximumAbsoluteRollDegrees = template.MaximumAbsoluteRollDegrees != defaults.MaximumAbsoluteRollDegrees
                ? template.MaximumAbsoluteRollDegrees
                : baseline.MaximumAbsoluteRollDegrees,
        };
    }
}
