using System.Diagnostics;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Domain.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Abhyanvaya.Infrastructure.InsightFace;

/// <summary>
/// InsightFace ONNX pipeline: SCRFD detection → 5-point alignment → ArcFace embedding.
/// </summary>
public sealed class InsightFaceEngine
{
    private readonly InsightFaceOnnxModelHost _modelHost;
    private readonly InsightFaceOptions _options;
    private readonly ILogger<InsightFaceEngine> _logger;

    public InsightFaceEngine(
        InsightFaceOnnxModelHost modelHost,
        IOptions<InsightFaceOptions> options,
        ILogger<InsightFaceEngine> logger)
    {
        _modelHost = modelHost;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FaceDetectionResponse> DetectAsync(
        FaceDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var image = Image.Load<Rgb24>(request.ImageBytes);
        var candidates = DetectFaces(image);
        var maxFaces = request.MaxFaces ?? int.MaxValue;

        var faces = new List<DetectedFaceDto>();
        var faceIndex = 1;

        foreach (var candidate in candidates.Take(maxFaces))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var aligned = InsightFaceImageMath.AlignFace(image, candidate.Landmarks, _options.RecognitionInputSize);
            var embedding = ExtractEmbedding(aligned);
            var bbox = InsightFaceImageMath.ToBoundingBox(candidate);

            byte[]? alignedBytes = null;
            await using (var ms = new MemoryStream())
            {
                await aligned.SaveAsWebpAsync(ms, cancellationToken);
                alignedBytes = ms.ToArray();
            }

            faces.Add(new DetectedFaceDto
            {
                FaceIndex = faceIndex++,
                DetectionScore = candidate.Score,
                BoundingBoxX = bbox.X,
                BoundingBoxY = bbox.Y,
                BoundingBoxWidth = bbox.Width,
                BoundingBoxHeight = bbox.Height,
                Landmarks = candidate.Landmarks,
                Embedding = embedding,
                EmbeddingDimension = embedding.Length,
                AlignedFaceBytes = alignedBytes
            });
        }

        stopwatch.Stop();

        return new FaceDetectionResponse
        {
            Provider = EmbeddingProviders.InsightFace,
            Model = _options.RecognitionModelFile,
            Version = _options.PipelineVersion,
            ImageWidth = image.Width,
            ImageHeight = image.Height,
            DetectionDurationMs = (int)stopwatch.ElapsedMilliseconds,
            Faces = faces
        };
    }

    public float[] GenerateSingleFaceEmbedding(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        var candidates = DetectFaces(image);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No face detected in the student photo.");
        }

        var best = candidates.OrderByDescending(c => c.Score).First();
        using var aligned = InsightFaceImageMath.AlignFace(image, best.Landmarks, _options.RecognitionInputSize);
        return ExtractEmbedding(aligned);
    }

    private IReadOnlyList<InsightFaceImageMath.FaceCandidate> DetectFaces(Image<Rgb24> image)
    {
        var session = _modelHost.GetDetectionSession();
        var inputSize = _options.DetectionInputSize;
        var inputTensor = InsightFaceImageMath.BuildDetectionInput(image, inputSize, out var scale, out var padX, out var padY);
        var inputName = session.InputMetadata.Keys.First();

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
        using var outputs = session.Run(inputs);

        var candidates = ParseDetectionOutputs(outputs, inputSize, scale, padX, padY, image.Width, image.Height);
        return InsightFaceImageMath.ApplyNms(candidates, _options.NmsThreshold);
    }

    private float[] ExtractEmbedding(Image<Rgb24> alignedFace)
    {
        var session = _modelHost.GetRecognitionSession();
        var inputTensor = InsightFaceImageMath.BuildRecognitionInput(alignedFace);
        var inputName = session.InputMetadata.Keys.First();

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
        using var outputs = session.Run(inputs);
        var embedding = outputs.First().AsEnumerable<float>().ToArray();
        return InsightFaceImageMath.L2Normalize(embedding);
    }

    private List<InsightFaceImageMath.FaceCandidate> ParseDetectionOutputs(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        int inputSize,
        float scale,
        int padX,
        int padY,
        int originalWidth,
        int originalHeight)
    {
        var candidates = new List<InsightFaceImageMath.FaceCandidate>();
        var strides = new[] { 8, 16, 32 };
        var outputList = outputs.ToList();

        // SCRFD (det_10g.onnx) emits its 9 output tensors GROUPED BY TENSOR TYPE across the three
        // strides — NOT interleaved (score, box, kps) per stride. For strides [8, 16, 32]:
        //   outputs[0..2] = scores    (shape [cells * anchors, 1])
        //   outputs[3..5] = box dists (shape [cells * anchors, 4]) — distances (l, t, r, b)
        //   outputs[6..8] = landmarks (shape [cells * anchors, 10]) — 5 points (x, y)
        // where cells = (inputSize / stride)^2. Each feature-map cell carries TWO anchors, laid out
        // anchor-major: row = (y * featureWidth + x) * numAnchors + anchor. Both anchors of a cell
        // share the same anchor centre (x * stride, y * stride).
        const int numAnchors = 2;
        const int scoreGroup = 0;
        var boxGroup = strides.Length;          // outputs[3..5]
        var landmarkGroup = strides.Length * 2; // outputs[6..8]

        if (outputList.Count < strides.Length * 3)
        {
            _logger.LogWarning(
                "SCRFD detection model returned {OutputCount} outputs; expected at least {Expected}. " +
                "Verify the det_10g.onnx model format.",
                outputList.Count,
                strides.Length * 3);
            return candidates;
        }

        for (var strideIndex = 0; strideIndex < strides.Length; strideIndex++)
        {
            var scores = outputList[scoreGroup + strideIndex].AsEnumerable<float>().ToArray();
            var boxes = outputList[boxGroup + strideIndex].AsEnumerable<float>().ToArray();
            var landmarks = outputList[landmarkGroup + strideIndex].AsEnumerable<float>().ToArray();
            var stride = strides[strideIndex];
            var featureHeight = inputSize / stride;
            var featureWidth = inputSize / stride;

            for (var y = 0; y < featureHeight; y++)
            {
                for (var x = 0; x < featureWidth; x++)
                {
                    var cellIndex = y * featureWidth + x;
                    for (var anchor = 0; anchor < numAnchors; anchor++)
                    {
                        var row = cellIndex * numAnchors + anchor;
                        if (row >= scores.Length)
                        {
                            continue;
                        }

                        // Confidence is read ONLY from the score tensor. SCRFD scores are sigmoid
                        // activations and therefore always lie in [0, 1]; a value outside that range
                        // means a regression tensor is being misread as a score (see AI11.FIX.1.3).
                        var score = scores[row];
                        Debug.Assert(
                            score >= -0.001f && score <= 1.001f,
                            $"SCRFD confidence out of [0,1] range: {score} (stride {stride}, row {row}). " +
                            "A non-score tensor is being interpreted as confidence.");

                        if (score < _options.DetectionThreshold)
                        {
                            continue;
                        }

                        var boxOffset = row * 4;
                        if (boxOffset + 3 >= boxes.Length)
                        {
                            continue;
                        }

                        var anchorX = (x * stride) - padX;
                        var anchorY = (y * stride) - padY;
                        var x1 = (anchorX - boxes[boxOffset] * stride) / scale;
                        var y1 = (anchorY - boxes[boxOffset + 1] * stride) / scale;
                        var x2 = (anchorX + boxes[boxOffset + 2] * stride) / scale;
                        var y2 = (anchorY + boxes[boxOffset + 3] * stride) / scale;

                        x1 = Math.Clamp(x1, 0, originalWidth - 1);
                        y1 = Math.Clamp(y1, 0, originalHeight - 1);
                        x2 = Math.Clamp(x2, x1 + 1, originalWidth);
                        y2 = Math.Clamp(y2, y1 + 1, originalHeight);

                        var landmarkOffset = row * 10;
                        var landmarkPoints = new float[10];
                        if (landmarkOffset + 9 < landmarks.Length)
                        {
                            for (var i = 0; i < 5; i++)
                            {
                                landmarkPoints[i * 2] = ((x * stride) + landmarks[landmarkOffset + i * 2] * stride - padX) / scale;
                                landmarkPoints[i * 2 + 1] = ((y * stride) + landmarks[landmarkOffset + i * 2 + 1] * stride - padY) / scale;
                            }
                        }

                        candidates.Add(new InsightFaceImageMath.FaceCandidate(score, x1, y1, x2, y2, landmarkPoints));
                    }
                }
            }
        }

        if (candidates.Count == 0)
        {
            _logger.LogWarning(
                "SCRFD output parsing produced zero candidates from {OutputCount} tensors; verify InsightFace detection model format.",
                outputList.Count);
        }

        return candidates;
    }
}
