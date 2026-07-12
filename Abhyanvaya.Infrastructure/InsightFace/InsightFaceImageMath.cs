using Abhyanvaya.Infrastructure.Diagnostics;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.Infrastructure.InsightFace;

internal static class InsightFaceImageMath
{
    internal sealed record FaceCandidate(
        float Score,
        float X1,
        float Y1,
        float X2,
        float Y2,
        float[] Landmarks);

    private static readonly (float X, float Y)[] ArcFaceTemplate =
    [
        (38.2946f, 51.6963f),
        (73.5318f, 51.5014f),
        (56.0252f, 71.7366f),
        (41.5493f, 92.3655f),
        (70.7299f, 92.2041f)
    ];

    public static DenseTensor<float> BuildDetectionInput(
        Image<Rgb24> image,
        int inputSize,
        out float scale,
        out int padX,
        out int padY,
        IRecognitionForensicsAudit? forensics = null)
    {
        scale = Math.Min((float)inputSize / image.Width, (float)inputSize / image.Height);
        var resizedWidth = (int)Math.Round(image.Width * scale);
        var resizedHeight = (int)Math.Round(image.Height * scale);
        padX = (inputSize - resizedWidth) / 2;
        padY = (inputSize - resizedHeight) / 2;

        using var working = image.Clone();
        // AI17.RUNTIME.2/.4: diagnostics-only object-lifetime tracking for the resize clone —
        // `forensics` only reads/logs and never influences the resize/crop below.
        forensics?.ObjectCreated("ImageSharp Clone", "detection-resize clone", resizedWidth, resizedHeight, "Rgb24");
        working.Mutate(ctx => ctx.Resize(resizedWidth, resizedHeight));
        var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize });

        for (var y = 0; y < inputSize; y++)
        {
            for (var x = 0; x < inputSize; x++)
            {
                var srcX = x - padX;
                var srcY = y - padY;
                if (srcX < 0 || srcY < 0 || srcX >= resizedWidth || srcY >= resizedHeight)
                {
                    continue;
                }

                var pixel = working[srcX, srcY];
                tensor[0, 0, y, x] = (pixel.R - 127.5f) / 128f;
                tensor[0, 1, y, x] = (pixel.G - 127.5f) / 128f;
                tensor[0, 2, y, x] = (pixel.B - 127.5f) / 128f;
            }
        }

        forensics?.ObjectDisposed("ImageSharp Clone", "detection-resize clone");
        return tensor;
    }

    public static Image<Rgb24> AlignFace(Image<Rgb24> source, float[] landmarks, int outputSize)
    {
        var (scale, translateX, translateY) = EstimateSimilarityTransform(landmarks, ArcFaceTemplate);
        var aligned = new Image<Rgb24>(outputSize, outputSize);

        for (var y = 0; y < outputSize; y++)
        {
            for (var x = 0; x < outputSize; x++)
            {
                var srcX = (x - translateX) / scale;
                var srcY = (y - translateY) / scale;
                aligned[x, y] = SampleBilinear(source, srcX, srcY);
            }
        }

        return aligned;
    }

    public static DenseTensor<float> BuildRecognitionInput(Image<Rgb24> alignedFace)
    {
        var size = alignedFace.Width;
        return BuildRecognitionInput(alignedFace, new float[3 * size * size]);
    }

    /// <summary>
    /// Same fill logic as <see cref="BuildRecognitionInput(Image{Rgb24})"/>, but writes into a
    /// caller-supplied buffer instead of allocating a fresh <c>float[]</c> (AI16.RUNTIME.3 — lets
    /// <c>InsightFaceEngine.ExtractEmbedding</c> reuse an <see cref="System.Buffers.ArrayPool{T}"/>
    /// rental across faces in the same classroom photo). Safe to pool because every element of
    /// <paramref name="buffer"/> is written unconditionally below — unlike
    /// <see cref="BuildDetectionInput"/>, there is no padding region left at its prior (possibly
    /// stale, if pooled) value, so recognition output is identical to the non-pooled overload.
    /// </summary>
    public static DenseTensor<float> BuildRecognitionInput(Image<Rgb24> alignedFace, Memory<float> buffer)
    {
        var size = alignedFace.Width;
        var tensor = new DenseTensor<float>(buffer, new[] { 1, 3, size, size });
        alignedFace.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y, x] = (pixel.R - 127.5f) / 127.5f;
                    tensor[0, 1, y, x] = (pixel.G - 127.5f) / 127.5f;
                    tensor[0, 2, y, x] = (pixel.B - 127.5f) / 127.5f;
                }
            }
        });

        return tensor;
    }

    public static float[] L2Normalize(float[] vector)
    {
        var magnitude = 0f;
        for (var i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = MathF.Sqrt(magnitude);
        if (magnitude <= float.Epsilon)
        {
            return vector;
        }

        var normalized = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }

        return normalized;
    }

    public static IReadOnlyList<FaceCandidate> ApplyNms(IReadOnlyList<FaceCandidate> candidates, float threshold)
    {
        var ordered = candidates.OrderByDescending(c => c.Score).ToList();
        var kept = new List<FaceCandidate>();

        while (ordered.Count > 0)
        {
            var current = ordered[0];
            kept.Add(current);
            ordered.RemoveAt(0);
            ordered = ordered
                .Where(c => IoU(current, c) < threshold)
                .ToList();
        }

        return kept;
    }

    public static (int X, int Y, int Width, int Height) ToBoundingBox(FaceCandidate candidate)
    {
        var x1 = (int)MathF.Floor(candidate.X1);
        var y1 = (int)MathF.Floor(candidate.Y1);
        var x2 = (int)MathF.Ceiling(candidate.X2);
        var y2 = (int)MathF.Ceiling(candidate.Y2);
        return (x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1));
    }

    private static float IoU(FaceCandidate a, FaceCandidate b)
    {
        var x1 = MathF.Max(a.X1, b.X1);
        var y1 = MathF.Max(a.Y1, b.Y1);
        var x2 = MathF.Min(a.X2, b.X2);
        var y2 = MathF.Min(a.Y2, b.Y2);
        var intersection = MathF.Max(0, x2 - x1) * MathF.Max(0, y2 - y1);
        var areaA = MathF.Max(0, a.X2 - a.X1) * MathF.Max(0, a.Y2 - a.Y1);
        var areaB = MathF.Max(0, b.X2 - b.X1) * MathF.Max(0, b.Y2 - b.Y1);
        var union = areaA + areaB - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static (float Scale, float TranslateX, float TranslateY) EstimateSimilarityTransform(
        float[] sourceLandmarks,
        (float X, float Y)[] destinationTemplate)
    {
        var src = new float[10];
        Array.Copy(sourceLandmarks, src, Math.Min(sourceLandmarks.Length, 10));

        var srcMeanX = Enumerable.Range(0, 5).Select(i => src[i * 2]).Average();
        var srcMeanY = Enumerable.Range(0, 5).Select(i => src[i * 2 + 1]).Average();
        var dstMeanX = destinationTemplate.Average(p => p.X);
        var dstMeanY = destinationTemplate.Average(p => p.Y);

        float numerator = 0, denominator = 0;
        for (var i = 0; i < 5; i++)
        {
            var sx = src[i * 2] - srcMeanX;
            var sy = src[i * 2 + 1] - srcMeanY;
            var dx = destinationTemplate[i].X - dstMeanX;
            var dy = destinationTemplate[i].Y - dstMeanY;
            numerator += sx * dx + sy * dy;
            denominator += sx * sx + sy * sy;
        }

        var scale = denominator <= float.Epsilon ? 1f : numerator / denominator;
        var translateX = dstMeanX - scale * srcMeanX;
        var translateY = dstMeanY - scale * srcMeanY;
        return (scale, translateX, translateY);
    }

    private static Rgb24 SampleBilinear(Image<Rgb24> image, float x, float y)
    {
        if (x < 0 || y < 0 || x >= image.Width - 1 || y >= image.Height - 1)
        {
            return default;
        }

        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = x0 + 1;
        var y1 = y0 + 1;
        var dx = x - x0;
        var dy = y - y0;

        var c00 = image[x0, y0];
        var c10 = image[x1, y0];
        var c01 = image[x0, y1];
        var c11 = image[x1, y1];

        byte Lerp(byte a, byte b, float t) => (byte)Math.Clamp(a + (b - a) * t, 0, 255);

        var topR = Lerp(c00.R, c10.R, dx);
        var topG = Lerp(c00.G, c10.G, dx);
        var topB = Lerp(c00.B, c10.B, dx);
        var bottomR = Lerp(c01.R, c11.R, dx);
        var bottomG = Lerp(c01.G, c11.G, dx);
        var bottomB = Lerp(c01.B, c11.B, dx);

        return new Rgb24(
            Lerp(topR, bottomR, dy),
            Lerp(topG, bottomG, dy),
            Lerp(topB, bottomB, dy));
    }
}
