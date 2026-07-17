using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Infrastructure.Enrollment.Validation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Validation;

public sealed class EnrollmentFaceQualityAnalyzerTests
{
    private static Image<Rgb24> CreateTestImage(bool blurry)
    {
        var image = new Image<Rgb24>(112, 112);
        image.Mutate(ctx =>
        {
            ctx.BackgroundColor(Color.White);
            if (blurry)
            {
                ctx.GaussianBlur(6);
            }
            else
            {
                for (var y = 0; y < image.Height; y += 8)
                {
                    for (var x = 0; x < image.Width; x += 8)
                    {
                        image[x, y] = ((x + y) % 16 == 0) ? Color.Black : Color.White;
                    }
                }
            }
        });

        return image;
    }

    [Fact]
    public void AnalyzeAlignedFace_SharpImage_HasHigherBlurScoreThanBlurryImage()
    {
        using var sharp = CreateTestImage(blurry: false);
        using var blurry = CreateTestImage(blurry: true);
        var landmarks = new float[] { 30, 40, 80, 40, 55, 70, 40, 95, 70, 95 };

        var sharpMetrics = EnrollmentFaceQualityAnalyzer.AnalyzeAlignedFace(sharp, landmarks);
        var blurryMetrics = EnrollmentFaceQualityAnalyzer.AnalyzeAlignedFace(blurry, landmarks);

        Assert.True(sharpMetrics.BlurScore > blurryMetrics.BlurScore);
    }

    [Fact]
    public void ComputeCompositeScore_ReturnsValueBetweenZeroAndOne()
    {
        var pose = new PoseEstimate { Yaw = 2, Pitch = 1, Roll = 1, Deviation = 2 };
        var score = EnrollmentFaceQualityAnalyzer.ComputeCompositeScore(
            0.95f,
            0.20f,
            250f,
            pose,
            new EnrollmentValidationOptions());

        Assert.InRange(score, 0f, 1f);
    }

    [Fact]
    public void EstimatePose_FrontalLandmarks_HasLowDeviation()
    {
        var landmarks = new float[] { 40, 50, 70, 50, 55, 65, 45, 85, 65, 85 };
        var pose = EnrollmentFaceQualityAnalyzer.EstimatePose(landmarks);

        Assert.True(Math.Abs(pose.Yaw) < 10);
        Assert.True(pose.Deviation < 15);
    }
}
