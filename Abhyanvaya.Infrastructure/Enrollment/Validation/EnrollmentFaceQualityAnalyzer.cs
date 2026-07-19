using Abhyanvaya.Application.Enrollment.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal static class EnrollmentFaceQualityAnalyzer
{
    internal sealed record FaceQualityMetrics(
        float BlurScore,
        float Brightness,
        float Contrast,
        PoseEstimate Pose);

    internal static FaceQualityMetrics AnalyzeAlignedFace(Image<Rgb24> alignedFace, float[] landmarks)
    {
        var lumaValues = ExtractLumaValues(alignedFace);
        var brightness = lumaValues.Count == 0 ? 0f : lumaValues.Average() / 255f;
        var contrast = ComputeNormalizedContrast(lumaValues);
        var blurScore = ComputeVarianceOfLaplacian(alignedFace);
        var pose = EstimatePose(landmarks);

        return new FaceQualityMetrics(blurScore, brightness, contrast, pose);
    }

    internal static FaceQualityMetrics AnalyzeFromWebpBytes(byte[] alignedWebpBytes, float[] landmarks)
    {
        using var image = Image.Load<Rgb24>(alignedWebpBytes);
        return AnalyzeAlignedFace(image, landmarks);
    }

    internal static float ComputeCompositeScore(
        float detectionConfidence,
        float faceSizeRatio,
        float blurScore,
        PoseEstimate pose,
        EnrollmentValidationOptions options)
    {
        var detectionTerm = Clamp01(detectionConfidence);
        var faceAreaTerm = Clamp01(faceSizeRatio / (float)Math.Max(0.01, options.MinimumFaceCoverageRatio * 2));
        var sharpnessTerm = Clamp01(blurScore / (float)Math.Max(1.0, options.BlurNormalizationReference));
        var poseTerm = Clamp01(1f - (pose.Deviation / (float)Math.Max(1.0, options.MaximumPoseDeviationDegrees)));

        var weightSum = options.CompositeWeightDetection
                        + options.CompositeWeightFaceArea
                        + options.CompositeWeightSharpness
                        + options.CompositeWeightPose;

        if (weightSum <= 0)
        {
            return 0f;
        }

        var score = (options.CompositeWeightDetection * detectionTerm
                     + options.CompositeWeightFaceArea * faceAreaTerm
                     + options.CompositeWeightSharpness * sharpnessTerm
                     + options.CompositeWeightPose * poseTerm) / weightSum;

        return Clamp01((float)score);
    }

    private static List<float> ExtractLumaValues(Image<Rgb24> image)
    {
        var values = new List<float>(image.Width * image.Height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (ref var pixel in row)
                {
                    values.Add(0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B);
                }
            }
        });

        return values;
    }

    private static float ComputeNormalizedContrast(IReadOnlyList<float> lumaValues)
    {
        if (lumaValues.Count == 0)
        {
            return 0f;
        }

        var mean = lumaValues.Average();
        var variance = lumaValues.Sum(v => (v - mean) * (v - mean)) / lumaValues.Count;
        return (float)(Math.Sqrt(variance) / 255.0);
    }

    private static float ComputeVarianceOfLaplacian(Image<Rgb24> image)
    {
        if (image.Width < 3 || image.Height < 3)
        {
            return 0f;
        }

        var responses = new List<float>((image.Width - 2) * (image.Height - 2));
        for (var y = 1; y < image.Height - 1; y++)
        {
            for (var x = 1; x < image.Width - 1; x++)
            {
                var center = Luma(image[x, y]);
                var laplacian = -4 * center
                                + Luma(image[x - 1, y])
                                + Luma(image[x + 1, y])
                                + Luma(image[x, y - 1])
                                + Luma(image[x, y + 1]);
                responses.Add(laplacian);
            }
        }

        if (responses.Count == 0)
        {
            return 0f;
        }

        var mean = responses.Average();
        return responses.Sum(v => (v - mean) * (v - mean)) / responses.Count;
    }

    private static float Luma(Rgb24 pixel) =>
        0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B;

    internal static PoseEstimate EstimatePose(float[] landmarks)
    {
        if (landmarks.Length < 10)
        {
            return new PoseEstimate { Yaw = 0, Pitch = 0, Roll = 0, Deviation = 0 };
        }

        var leftEyeX = landmarks[0];
        var leftEyeY = landmarks[1];
        var rightEyeX = landmarks[2];
        var rightEyeY = landmarks[3];
        var noseX = landmarks[4];
        var noseY = landmarks[5];
        var leftMouthX = landmarks[6];
        var leftMouthY = landmarks[7];
        var rightMouthX = landmarks[8];
        var rightMouthY = landmarks[9];

        var roll = MathF.Atan2(rightEyeY - leftEyeY, rightEyeX - leftEyeX) * (180f / MathF.PI);

        var eyeMidX = (leftEyeX + rightEyeX) / 2f;
        var interEye = MathF.Max(1f, MathF.Abs(rightEyeX - leftEyeX));
        var yaw = ((noseX - eyeMidX) / interEye) * 45f;

        var eyeMidY = (leftEyeY + rightEyeY) / 2f;
        var mouthMidY = (leftMouthY + rightMouthY) / 2f;
        var eyeToNose = MathF.Max(1f, noseY - eyeMidY);
        var noseToMouth = MathF.Max(1f, mouthMidY - noseY);
        var pitch = ((eyeToNose - noseToMouth) / MathF.Max(eyeToNose, noseToMouth)) * 30f;

        var deviation = MathF.Sqrt(yaw * yaw + pitch * pitch + roll * roll) / MathF.Sqrt(3f);

        return new PoseEstimate
        {
            Yaw = yaw,
            Pitch = pitch,
            Roll = roll,
            Deviation = deviation,
        };
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
}
