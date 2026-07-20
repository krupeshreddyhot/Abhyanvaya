using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal static class EnrollmentValidationReportAggregator
{
    public static ValidationReport BuildReport(
        IReadOnlyList<EnrollmentValidationRuleResult> ruleResults,
        EnrollmentValidationRuleContext context,
        bool validationPassed,
        EnrollmentValidationTelemetry? telemetry)
    {
        var failures = ruleResults
            .Where(r => r.Severity == ValidationRuleOutcome.Fail)
            .Select(r => r.Message)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Cast<string>()
            .ToList();

        var warnings = ruleResults
            .Where(r => r.Severity == ValidationRuleOutcome.Warning)
            .Select(r => r.Message)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Cast<string>()
            .ToList();

        var information = ruleResults
            .Where(r => r.Severity == ValidationRuleOutcome.Information)
            .Select(r => r.Message)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Cast<string>()
            .ToList();

        var severitySummary = new ValidationSeveritySummary
        {
            PassCount = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.Pass),
            FailCount = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.Fail),
            WarningCount = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.Warning),
            InformationCount = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.Information),
            SkippedCount = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.Skipped),
            NotApplicableCount = ruleResults.Count(r => r.Severity == ValidationRuleOutcome.NotApplicable),
        };

        EnrollmentFaceAnalysisAccessor? accessor = context.AnalysisAccessor as EnrollmentFaceAnalysisAccessor;
        var analysis = accessor?.GetCachedAnalysis();

        int? sourceWidth = accessor?.SourceWidth;
        int? sourceHeight = accessor?.SourceHeight;
        var faceCount = analysis?.Faces.Count ?? 0;

        float? faceConfidence = null;
        int? faceWidth = null;
        int? faceHeight = null;
        float? faceSizeRatio = null;
        float? blurScore = null;
        PoseEstimate? pose = null;
        float? brightness = null;
        float? contrast = null;
        float? compositeScore = null;

        if (analysis?.Faces.Count == 1)
        {
            var face = analysis.Faces[0];
            faceConfidence = face.DetectionScore;
            faceWidth = face.BoundingBoxWidth;
            faceHeight = face.BoundingBoxHeight;
            var imageArea = Math.Max(1, analysis.ImageWidth * analysis.ImageHeight);
            faceSizeRatio = (face.BoundingBoxWidth * face.BoundingBoxHeight) / (float)imageArea;

            var metrics = accessor?.GetCachedQualityMetrics();

            if (metrics is not null)
            {
                blurScore = metrics.BlurScore;
                brightness = metrics.Brightness;
                contrast = metrics.Contrast;
                pose = metrics.Pose;
                compositeScore = ComputeCompositeScore(face, faceSizeRatio.Value, metrics, context.Thresholds);
            }
        }
        else if (analysis?.Faces.Count > 1)
        {
            faceConfidence = analysis.Faces.OrderByDescending(f => f.DetectionScore).First().DetectionScore;
        }

        return new ValidationReport
        {
            OverallResult = validationPassed ? ValidationOverallResult.Passed : ValidationOverallResult.Failed,
            OverallScore = compositeScore,
            FaceCount = context.AnalysisAccessor.IsDetectionSkipped ? 0 : faceCount,
            FaceConfidence = faceConfidence,
            DetectionConfidence = faceConfidence,
            BlurScore = blurScore,
            Pose = pose,
            Brightness = brightness,
            Contrast = contrast,
            SourceWidth = sourceWidth,
            SourceHeight = sourceHeight,
            FaceWidth = faceWidth,
            FaceHeight = faceHeight,
            FaceCoveragePercent = faceSizeRatio * 100f,
            FaceSizeRatio = faceSizeRatio,
            CompositeScore = compositeScore,
            EyesVisible = null,
            OcclusionCoverage = null,
            RuleResults = ruleResults.Select(MapRuleResult).ToList(),
            ValidationFailures = failures,
            Warnings = warnings,
            InformationMessages = information,
            SeveritySummary = severitySummary,
            Telemetry = telemetry,
            EmbeddingEligible = validationPassed
                && analysis?.AlignedFaceWebpBytes is { Length: > 0 },
        };
    }

    private static ValidationRuleResult MapRuleResult(EnrollmentValidationRuleResult result) =>
        new()
        {
            RuleId = result.RuleName,
            Outcome = result.Severity,
            Message = result.Message,
            MeasuredValue = result.MeasuredValue,
            ThresholdValue = result.ThresholdValue,
        };

    private static float? ComputeCompositeScore(
        EnrollmentDetectedFace face,
        float faceSizeRatio,
        FaceQualityMetrics metrics,
        EnrollmentValidationThresholds thresholds)
    {
        var options = new EnrollmentValidationOptions
        {
            MinimumFaceCoverageRatio = thresholds.MinimumFaceCoverageRatio,
            BlurNormalizationReference = thresholds.BlurNormalizationReference,
            MaximumPoseDeviationDegrees = thresholds.MaximumPoseDeviationDegrees,
            CompositeWeightDetection = thresholds.CompositeWeightDetection,
            CompositeWeightFaceArea = thresholds.CompositeWeightFaceArea,
            CompositeWeightSharpness = thresholds.CompositeWeightSharpness,
            CompositeWeightPose = thresholds.CompositeWeightPose,
        };

        return EnrollmentFaceQualityAnalyzer.ComputeCompositeScore(
            face.DetectionScore,
            faceSizeRatio,
            metrics.BlurScore,
            metrics.Pose,
            options);
    }
}
