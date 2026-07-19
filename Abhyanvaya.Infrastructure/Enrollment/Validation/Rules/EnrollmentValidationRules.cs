using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation.Rules;

internal sealed class ImageFormatRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.ImageFormat;
    public override int Order => 10;

    protected override Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var result = context.AnalysisAccessor.ValidateFormat();
        if (result.IsValid)
        {
            return Task.FromResult(Pass());
        }

        return Task.FromResult(Fail(
            result.FailureMessage ?? "Unsupported image format.",
            EnrollmentValidationDiagnosticCodes.UnsupportedFormat,
            FailureCategory.InvalidImage));
    }
}

internal sealed class CorruptImageRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.CorruptImage;
    public override int Order => 20;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        if (context.AnalysisAccessor.FormatResult is { IsValid: false })
        {
            return NotApplicable("Image format check failed.");
        }

        var decode = await context.AnalysisAccessor.GetDecodeResultAsync(cancellationToken);
        if (decode is null)
        {
            return NotApplicable("Decode skipped.");
        }

        if (decode.IsValid)
        {
            return Pass();
        }

        var category = decode.IsUnsupportedFormat
            ? FailureCategory.InvalidImage
            : FailureCategory.CorruptImage;
        var code = decode.IsUnsupportedFormat
            ? EnrollmentValidationDiagnosticCodes.UnsupportedFormat
            : EnrollmentValidationDiagnosticCodes.CorruptImage;

        context.AnalysisAccessor.MarkDetectionSkipped();

        return Fail(
            decode.FailureMessage ?? "Image could not be decoded.",
            code,
            category);
    }
}

internal sealed class MinimumResolutionRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.MinimumSourceResolution;
    public override int Order => 30;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var decode = await context.AnalysisAccessor.GetDecodeResultAsync(cancellationToken);
        if (decode is null || decode.Width is null || decode.Height is null)
        {
            return NotApplicable("Source dimensions unavailable.");
        }

        var thresholds = context.Thresholds;
        var pass = decode.Width >= thresholds.MinimumSourceWidth
                   && decode.Height >= thresholds.MinimumSourceHeight;

        if (pass)
        {
            return Pass(measuredValue: decode.Width, thresholdValue: thresholds.MinimumSourceWidth);
        }

        context.AnalysisAccessor.MarkDetectionSkipped();

        return Fail(
            $"Source image must be at least {thresholds.MinimumSourceWidth}×{thresholds.MinimumSourceHeight} pixels.",
            EnrollmentValidationDiagnosticCodes.SourceResTooLow,
            FailureCategory.LowResolutionRejected,
            decode.Width,
            thresholds.MinimumSourceWidth);
    }
}

internal sealed class MaximumResolutionRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.MaximumSourceResolution;
    public override int Order => 40;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var decode = await context.AnalysisAccessor.GetDecodeResultAsync(cancellationToken);
        if (decode is null || decode.Width is null || decode.Height is null)
        {
            return NotApplicable("Source dimensions unavailable.");
        }

        var thresholds = context.Thresholds;
        var pass = decode.Width <= thresholds.MaximumSourceWidth
                   && decode.Height <= thresholds.MaximumSourceHeight;

        if (pass)
        {
            return Pass(measuredValue: decode.Width, thresholdValue: thresholds.MaximumSourceWidth);
        }

        context.AnalysisAccessor.MarkDetectionSkipped();

        return Fail(
            $"Source image must not exceed {thresholds.MaximumSourceWidth}×{thresholds.MaximumSourceHeight} pixels.",
            EnrollmentValidationDiagnosticCodes.SourceResTooHigh,
            FailureCategory.LowResolutionRejected,
            decode.Width,
            thresholds.MaximumSourceWidth);
    }
}

internal sealed class ExactlyOneFaceRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.ExactlyOneFace;
    public override int Order => 50;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        if (context.AnalysisAccessor.IsDetectionSkipped)
        {
            return NotApplicable("Face detection skipped.");
        }

        var analysis = await context.AnalysisAccessor.GetAnalysisAsync(cancellationToken);
        if (analysis is null)
        {
            return NotApplicable("Face detection skipped.");
        }

        return analysis.Faces.Count switch
        {
            1 => Pass(measuredValue: 1),
            0 => Fail(
                "No face detected.",
                EnrollmentValidationDiagnosticCodes.NoFace,
                FailureCategory.NoFaceDetected,
                0),
            _ => Fail(
                "Multiple faces detected; enrollment requires exactly one.",
                EnrollmentValidationDiagnosticCodes.MultipleFaces,
                FailureCategory.MultipleFacesDetected,
                analysis.Faces.Count),
        };
    }
}

internal sealed class FaceConfidenceRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.FaceConfidence;
    public override int Order => 55;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        if (context.AnalysisAccessor.IsDetectionSkipped)
        {
            return NotApplicable("Face detection skipped.");
        }

        var analysis = await context.AnalysisAccessor.GetAnalysisAsync(cancellationToken);
        if (analysis is null || analysis.Faces.Count == 0)
        {
            return NotApplicable("No face detected.");
        }

        var topFace = analysis.Faces.OrderByDescending(f => f.DetectionScore).First();
        var threshold = context.Thresholds.DetectionConfidenceThreshold;
        if (topFace.DetectionScore >= threshold)
        {
            return Pass(measuredValue: topFace.DetectionScore, thresholdValue: threshold);
        }

        return Fail(
            $"Face detection confidence below minimum ({threshold:F2}).",
            EnrollmentValidationDiagnosticCodes.LowFaceConfidence,
            FailureCategory.NoFaceDetected,
            topFace.DetectionScore,
            threshold);
    }
}

internal sealed class MinimumFaceCropResolutionRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.MinimumFaceCropResolution;
    public override int Order => 60;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        if (context.AnalysisAccessor.IsDetectionSkipped)
        {
            return NotApplicable("Face detection skipped.");
        }

        var analysis = await context.AnalysisAccessor.GetAnalysisAsync(cancellationToken);
        if (analysis?.Faces.Count != 1)
        {
            return NotApplicable("Requires exactly one detected face.");
        }

        var face = analysis.Faces[0];
        var thresholds = context.Thresholds;
        var pass = face.BoundingBoxWidth >= thresholds.MinimumFaceWidth
                   && face.BoundingBoxHeight >= thresholds.MinimumFaceHeight;

        if (pass)
        {
            return Pass(measuredValue: face.BoundingBoxWidth, thresholdValue: thresholds.MinimumFaceWidth);
        }

        return Fail(
            $"Face crop must be at least {thresholds.MinimumFaceWidth}×{thresholds.MinimumFaceHeight} pixels.",
            EnrollmentValidationDiagnosticCodes.FaceCropTooSmall,
            FailureCategory.LowResolutionRejected,
            face.BoundingBoxWidth,
            thresholds.MinimumFaceWidth);
    }
}

internal sealed class FaceCoverageRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.FaceSizeCoverage;
    public override int Order => 70;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        if (context.AnalysisAccessor.IsDetectionSkipped)
        {
            return NotApplicable("Face detection skipped.");
        }

        var analysis = await context.AnalysisAccessor.GetAnalysisAsync(cancellationToken);
        if (analysis?.Faces.Count != 1)
        {
            return NotApplicable("Requires exactly one detected face.");
        }

        var face = analysis.Faces[0];
        var imageArea = Math.Max(1, analysis.ImageWidth * analysis.ImageHeight);
        var ratio = (face.BoundingBoxWidth * face.BoundingBoxHeight) / (float)imageArea;
        var threshold = context.Thresholds.MinimumFaceCoverageRatio;

        if (ratio >= threshold)
        {
            return Pass(measuredValue: ratio, thresholdValue: threshold);
        }

        return Fail(
            $"Face occupies {ratio:P1} of the image; minimum is {threshold:P1}.",
            EnrollmentValidationDiagnosticCodes.FaceTooSmallInFrame,
            FailureCategory.LowResolutionRejected,
            ratio,
            threshold);
    }
}

internal sealed class BlurRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.BlurScore;
    public override int Order => 80;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var metrics = await context.AnalysisAccessor.GetQualityMetricsAsync(cancellationToken);
        if (metrics is null)
        {
            return NotApplicable("Requires exactly one detected face.");
        }

        var threshold = context.Thresholds.BlurThreshold;
        if (metrics.BlurScore >= threshold)
        {
            return Pass(measuredValue: metrics.BlurScore, thresholdValue: threshold);
        }

        return Fail(
            $"Image is too blurry (score {metrics.BlurScore:F0}, minimum {threshold:F0}).",
            EnrollmentValidationDiagnosticCodes.BlurRejected,
            FailureCategory.BlurRejected,
            metrics.BlurScore,
            threshold);
    }
}

internal sealed class PoseRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.Pose;
    public override int Order => 90;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var metrics = await context.AnalysisAccessor.GetQualityMetricsAsync(cancellationToken);
        if (metrics is null)
        {
            return NotApplicable("Requires exactly one detected face.");
        }

        var thresholds = context.Thresholds;
        var pose = metrics.Pose;
        var pass = Math.Abs(pose.Yaw) <= thresholds.MaximumAbsoluteYawDegrees
                   && Math.Abs(pose.Pitch) <= thresholds.MaximumAbsolutePitchDegrees
                   && Math.Abs(pose.Roll) <= thresholds.MaximumAbsoluteRollDegrees;

        if (pass)
        {
            return Pass(measuredValue: pose.Deviation, thresholdValue: thresholds.MaximumPoseDeviationDegrees);
        }

        return Fail(
            $"Head pose exceeds allowed limits (yaw {pose.Yaw:F0}°, pitch {pose.Pitch:F0}°, roll {pose.Roll:F0}°).",
            EnrollmentValidationDiagnosticCodes.PoseRejected,
            FailureCategory.BlurRejected,
            pose.Deviation,
            thresholds.MaximumPoseDeviationDegrees);
    }
}

internal sealed class BrightnessRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.Brightness;
    public override int Order => 100;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var metrics = await context.AnalysisAccessor.GetQualityMetricsAsync(cancellationToken);
        if (metrics is null)
        {
            return NotApplicable("Requires exactly one detected face.");
        }

        var thresholds = context.Thresholds;
        var pass = metrics.Brightness >= thresholds.MinimumBrightness
                   && metrics.Brightness <= thresholds.MaximumBrightness;

        if (pass)
        {
            return Pass(measuredValue: metrics.Brightness, thresholdValue: thresholds.MinimumBrightness);
        }

        return Fail(
            $"Face brightness {metrics.Brightness:F2} outside acceptable range [{thresholds.MinimumBrightness:F2}, {thresholds.MaximumBrightness:F2}].",
            EnrollmentValidationDiagnosticCodes.BrightnessRejected,
            FailureCategory.BlurRejected,
            metrics.Brightness,
            thresholds.MinimumBrightness);
    }
}

internal sealed class ContrastRule : EnrollmentValidationRuleBase
{
    public override string Name => EnrollmentValidationRuleIds.Contrast;
    public override int Order => 110;

    protected override async Task<EnrollmentValidationRuleResult> ExecuteCoreAsync(
        EnrollmentValidationRuleContext context,
        CancellationToken cancellationToken)
    {
        var metrics = await context.AnalysisAccessor.GetQualityMetricsAsync(cancellationToken);
        if (metrics is null)
        {
            return NotApplicable("Requires exactly one detected face.");
        }

        var threshold = context.Thresholds.MinimumContrast;
        if (metrics.Contrast >= threshold)
        {
            return Pass(measuredValue: metrics.Contrast, thresholdValue: threshold);
        }

        return Fail(
            $"Face contrast {metrics.Contrast:F2} below minimum {threshold:F2}.",
            EnrollmentValidationDiagnosticCodes.ContrastRejected,
            FailureCategory.BlurRejected,
            metrics.Contrast,
            threshold);
    }
}
