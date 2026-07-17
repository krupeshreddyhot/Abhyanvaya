using System.Buffers;
using System.Diagnostics;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Domain.Constants;
using Abhyanvaya.Infrastructure.Diagnostics;
using Abhyanvaya.Infrastructure.Diagnostics.MemoryAudit;
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
    private readonly IRecognitionPipelineDiagnostics _diagnostics;
    private readonly IRecognitionForensicsAudit _forensics;
    private readonly IRecognitionMemoryAudit _memoryAudit;
    private readonly ILogger<InsightFaceEngine> _logger;

    public InsightFaceEngine(
        InsightFaceOnnxModelHost modelHost,
        IOptions<InsightFaceOptions> options,
        IRecognitionPipelineDiagnostics diagnostics,
        IRecognitionForensicsAudit forensics,
        IRecognitionMemoryAudit memoryAudit,
        ILogger<InsightFaceEngine> logger)
    {
        _modelHost = modelHost;
        _options = options.Value;
        _diagnostics = diagnostics;
        _forensics = forensics;
        _memoryAudit = memoryAudit;
        _logger = logger;
    }

    public async Task<FaceDetectionResponse> DetectAsync(
        FaceDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // AI15.DIAGNOSTICS.1: diagnostics calls only read process/GC state and log — they never
        // influence the detection/alignment/embedding logic below, which is byte-for-byte unchanged.
        var decodeStage = _diagnostics.StageStart("Decode Image");
        _forensics.Checkpoint("Image Decode Started");
        _memoryAudit.Snapshot("Image Decode Started");
        using var image = Image.Load<Rgb24>(request.ImageBytes);
        _diagnostics.StageEnd(decodeStage);
        _forensics.Checkpoint("Image Decode Finished");
        _memoryAudit.Snapshot("Image Decode Finished");
        _diagnostics.ObjectCreated("ImageSharp Image", "source image");
        _forensics.ObjectCreated("ImageSharp Image", "source image", image.Width, image.Height, "Rgb24", (long)image.Width * image.Height * 3);
        var sourceImageBytes = (long)image.Width * image.Height * 3;
        var sourceImageObjectId = _memoryAudit.RegisterObject("ImageSharp Image", sourceImageBytes, "Image Decode Finished");

        var detectionStage = _diagnostics.StageStart("Face Detection");
        _forensics.Checkpoint("Face Detection Started");
        _memoryAudit.Snapshot("Face Detection Started");
        var candidates = DetectFaces(image);
        _diagnostics.StageEnd(detectionStage);
        _forensics.Checkpoint("Face Detection Finished");
        _memoryAudit.Snapshot("Face Detection Finished");

        var maxFaces = request.MaxFaces ?? int.MaxValue;
        var selectedCandidates = candidates.Take(maxFaces).ToList();
        var faceCount = selectedCandidates.Count;

        var faces = new List<DetectedFaceDto>();
        var faceIndex = 1;

        _forensics.Checkpoint("Face Crop Loop Begin");
        _memoryAudit.Snapshot("Face Crop Loop Begin");

        foreach (var candidate in selectedCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentFace = faceIndex;

            var croppingStage = _diagnostics.StageStart("Face Cropping", currentFace, faceCount);
            _forensics.Checkpoint("Before Face Crop", currentFace);
            _memoryAudit.Snapshot("Before Face Crop", currentFace);
            using var aligned = InsightFaceImageMath.AlignFace(image, candidate.Landmarks, _options.RecognitionInputSize);
            _diagnostics.StageEnd(croppingStage);
            _forensics.Checkpoint("After Face Crop", currentFace);
            _memoryAudit.Snapshot("After Face Crop", currentFace);
            _diagnostics.ObjectCreated("ImageSharp Image", $"aligned face {currentFace}");
            _forensics.ObjectCreated("ImageSharp Image", $"aligned face {currentFace}", aligned.Width, aligned.Height, "Rgb24", (long)aligned.Width * aligned.Height * 3);
            var alignedFaceBytesEstimate = (long)aligned.Width * aligned.Height * 3;
            var alignedFaceObjectId = _memoryAudit.RegisterObject("Face Crop", alignedFaceBytesEstimate, "After Face Crop", currentFace);

            var embeddingStage = _diagnostics.StageStart("Embedding Generation", currentFace, faceCount);
            _forensics.Checkpoint("Before Embedding Generation", currentFace);
            _memoryAudit.Snapshot("Before Embedding Generation", currentFace);
            var embedding = ExtractEmbedding(aligned);
            _diagnostics.StageEnd(embeddingStage);
            _forensics.Checkpoint("After Embedding Generation", currentFace);
            _memoryAudit.Snapshot("After Embedding Generation", currentFace);
            _forensics.CheckFaceCropRetainedAfterEmbedding($"aligned face {currentFace}");
            var embeddingObjectId = _memoryAudit.RegisterObject("Embedding Array", embedding.Length * (long)sizeof(float), "After Embedding Generation", currentFace);
            _memoryAudit.DisposeObject(embeddingObjectId);

            var bbox = InsightFaceImageMath.ToBoundingBox(candidate);

            byte[]? alignedBytes = null;
            _diagnostics.ObjectCreated("MemoryStream", $"face {currentFace} webp buffer");
            // AI16.RUNTIME.2: a small starting capacity avoids a few of MemoryStream's
            // doubling-and-copying reallocations while the WebP encoder writes a small aligned-face
            // crop — output bytes are unaffected either way.
            var webpStreamObjectId = _memoryAudit.RegisterObject("MemoryStream", 8192, "After Embedding Generation", currentFace);
            await using (var ms = new MemoryStream(8192))
            {
                await aligned.SaveAsWebpAsync(ms, cancellationToken);
                alignedBytes = ms.ToArray();
            }
            _diagnostics.ObjectDisposed("MemoryStream", $"face {currentFace} webp buffer");
            _memoryAudit.DisposeObject(webpStreamObjectId);
            var thumbnailByteArrayId = _memoryAudit.RegisterObject("Byte Array", alignedBytes.Length, "After Thumbnail Encode", currentFace);
            _memoryAudit.Snapshot("After Thumbnail Encode", currentFace);

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
            // The DetectedFaceDto above now owns this byte[] for the remainder of the job (it survives
            // until ClassroomRecognitionPipeline's thumbnail-persistence loop uploads it) — tracked as
            // disposed here only in the sense that this method's local `thumbnailByteArrayId`
            // registration handle is retired; the actual bytes are re-registered/disposed around the
            // upload call in ClassroomRecognitionPipeline, where their real end-of-life happens.
            _memoryAudit.DisposeObject(thumbnailByteArrayId);

            // `aligned` is disposed immediately after this point by the `using var` above (its scope
            // is the remainder of this loop iteration) — logged just before that implicit dispose runs.
            _diagnostics.ObjectDisposed("ImageSharp Image", $"aligned face {currentFace}");
            _forensics.ObjectDisposed("ImageSharp Image", $"aligned face {currentFace}");
            _memoryAudit.DisposeObject(alignedFaceObjectId);
            _memoryAudit.Snapshot("After Dispose", currentFace);
            _diagnostics.FaceEvent("Dispose Complete", currentFace, faceCount);
        }

        _diagnostics.ObjectDisposed("ImageSharp Image", "source image");
        _forensics.ObjectDisposed("ImageSharp Image", "source image");
        _memoryAudit.DisposeObject(sourceImageObjectId);

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

    /// <summary>
    /// Detection + single-face alignment for enrollment validation — no embedding extraction.
    /// Reuses private <see cref="DetectFaces"/> and <see cref="InsightFaceImageMath.AlignFace"/>.
    /// </summary>
    public async Task<EnrollmentFaceAnalysisEngineResult> AnalyzeForEnrollmentValidationAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        using var image = Image.Load<Rgb24>(imageBytes);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = DetectFaces(image);
        var faces = new List<EnrollmentFaceAnalysisEngineFace>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var bbox = InsightFaceImageMath.ToBoundingBox(candidate);
            faces.Add(new EnrollmentFaceAnalysisEngineFace(
                candidate.Score,
                bbox.X,
                bbox.Y,
                bbox.Width,
                bbox.Height,
                candidate.Landmarks));
        }

        byte[]? alignedWebp = null;
        if (candidates.Count == 1)
        {
            using var aligned = InsightFaceImageMath.AlignFace(
                image,
                candidates[0].Landmarks,
                _options.RecognitionInputSize);
            await using var ms = new MemoryStream(8192);
            await aligned.SaveAsWebpAsync(ms, cancellationToken);
            alignedWebp = ms.ToArray();
        }

        return new EnrollmentFaceAnalysisEngineResult(image.Width, image.Height, faces, alignedWebp);
    }

    /// <summary>
    /// Generates an L2-normalized embedding from a pre-aligned face crop (typically 112×112 WebP).
    /// Skips detection — enrollment embedding consumes the stored aligned artifact directly.
    /// </summary>
    public float[] GenerateEmbeddingFromAlignedFace(Stream alignedFaceStream, CancellationToken cancellationToken = default)
    {
        using var image = Image.Load<Rgb24>(alignedFaceStream);
        cancellationToken.ThrowIfCancellationRequested();
        return ExtractEmbedding(image);
    }

    private IReadOnlyList<InsightFaceImageMath.FaceCandidate> DetectFaces(Image<Rgb24> image)
    {
        var session = _modelHost.GetDetectionSession();
        var inputSize = _options.DetectionInputSize;
        var inputTensor = InsightFaceImageMath.BuildDetectionInput(image, inputSize, out var scale, out var padX, out var padY, _forensics);
        _diagnostics.ObjectCreated("DenseTensor<float>", "detection input");
        _forensics.ObjectCreated("DenseTensor<float>", "detection input");
        var inputName = session.InputMetadata.Keys.First();

        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
        _diagnostics.ObjectCreated("NamedOnnxValue", "detection input");
        _forensics.ObjectCreated("NamedOnnxValue", "detection input");

        // AI16.RUNTIME.4: a dedicated "before inference"/"after inference" checkpoint, narrower than
        // the surrounding "Face Detection" stage (which also covers tensor building and output
        // parsing) — isolates the native ONNX Runtime Run() call's own memory footprint.
        var inferenceStage = _diagnostics.StageStart("ONNX Inference (Detection)");
        var onnxInferenceStopwatch = Stopwatch.StartNew();
        var beforeDetectionInference = RecognitionMemorySnapshot.Capture();
        var beforeDetectionInferenceAudit = CaptureRawMemoryAuditSnapshot("Before ONNX Detection Inference");
        using var outputs = session.Run(inputs);
        var afterDetectionInference = RecognitionMemorySnapshot.Capture();
        var afterDetectionInferenceAudit = CaptureRawMemoryAuditSnapshot("After ONNX Detection Inference");
        onnxInferenceStopwatch.Stop();
        _diagnostics.StageEnd(inferenceStage);
        _forensics.ObjectDisposed("NamedOnnxValue", "detection input");
        _forensics.ObjectDisposed("DenseTensor<float>", "detection input");
        _diagnostics.ObjectCreated("DisposableNamedOnnxValue collection", "detection outputs");
        _forensics.ObjectCreated("DisposableNamedOnnxValue collection", "detection outputs");

        var candidates = ParseDetectionOutputs(outputs, inputSize, scale, padX, padY, image.Width, image.Height);
        var result = InsightFaceImageMath.ApplyNms(candidates, _options.NmsThreshold);

        // `outputs` is disposed immediately after this method returns by the `using` above.
        _diagnostics.ObjectDisposed("DisposableNamedOnnxValue collection", "detection outputs");
        _forensics.ObjectDisposed("DisposableNamedOnnxValue collection", "detection outputs");
        // AI17.RUNTIME.5: recorded after `outputs` is logically done (disposal happens via the
        // `using` above when this method returns) — "Inference Session Reused"=true because
        // InsightFaceOnnxModelHost lazily creates the detection InferenceSession once and caches it
        // for the lifetime of the host; "Tensor Reused"=false because BuildDetectionInput allocates a
        // fresh DenseTensor per call (see AI16.RUNTIME.3 — only the recognition/embedding tensor below
        // uses a pooled buffer, not detection).
        _forensics.RecordOnnxInference(
            model: _options.DetectionModelFile,
            inputTensorShape: $"[{string.Join('x', inputTensor.Dimensions.ToArray())}]",
            outputTensorShape: $"{outputs.Count} tensors",
            inferenceDurationMs: onnxInferenceStopwatch.ElapsedMilliseconds,
            before: beforeDetectionInference,
            after: afterDetectionInference,
            inferenceSessionReused: true,
            tensorReused: false,
            disposableOutputCount: outputs.Count);
        _memoryAudit.RecordOnnxInference(
            model: _options.DetectionModelFile,
            inputTensorShape: $"[{string.Join('x', inputTensor.Dimensions.ToArray())}]",
            outputTensorShape: $"{outputs.Count} tensors",
            inputBytesApprox: inputTensor.Length * (long)sizeof(float),
            outputBytesApprox: outputs.Count * 4096L,
            before: beforeDetectionInferenceAudit,
            after: afterDetectionInferenceAudit,
            inferenceDurationMs: onnxInferenceStopwatch.ElapsedMilliseconds,
            disposableOutputCount: outputs.Count,
            outputsDisposed: true);
        return result;
    }

    private float[] ExtractEmbedding(Image<Rgb24> alignedFace)
    {
        var session = _modelHost.GetRecognitionSession();
        var size = alignedFace.Width;
        var length = 3 * size * size;

        // AI16.RUNTIME.3: rent the tensor's backing array from the shared pool instead of allocating
        // a fresh float[] per face — ExtractEmbedding runs once per detected face, sequentially,
        // within one classroom photo. Every element of this buffer is overwritten by
        // BuildRecognitionInput before it is read (see that method's remarks), so a possibly "dirty"
        // rented array never leaks stale data into the model input — output is identical to the
        // unpooled path. The rented array is only needed until session.Run(...) returns below, so it
        // is safe to return it immediately afterwards; `finally` guarantees the return even on
        // exception (no growth in outstanding rentals under repeated failures).
        var rented = ArrayPool<float>.Shared.Rent(length);
        try
        {
            var inputTensor = InsightFaceImageMath.BuildRecognitionInput(alignedFace, rented.AsMemory(0, length));
            _diagnostics.ObjectCreated("DenseTensor<float>", "recognition input (pooled)");
            _forensics.ObjectCreated("DenseTensor<float>", "recognition input (pooled)");
            var inputName = session.InputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, inputTensor) };
            _diagnostics.ObjectCreated("NamedOnnxValue", "recognition input");
            _forensics.ObjectCreated("NamedOnnxValue", "recognition input");

            // AI16.RUNTIME.4: see the matching comment in DetectFaces — isolates the native ONNX
            // Runtime Run() call's own memory footprint from the broader "Embedding Generation" stage.
            var inferenceStage = _diagnostics.StageStart("ONNX Inference (Embedding)");
            var onnxInferenceStopwatch = Stopwatch.StartNew();
            var beforeEmbeddingInference = RecognitionMemorySnapshot.Capture();
            var beforeEmbeddingInferenceAudit = CaptureRawMemoryAuditSnapshot("Before ONNX Embedding Inference");
            using var outputs = session.Run(inputs);
            var afterEmbeddingInference = RecognitionMemorySnapshot.Capture();
            var afterEmbeddingInferenceAudit = CaptureRawMemoryAuditSnapshot("After ONNX Embedding Inference");
            onnxInferenceStopwatch.Stop();
            _diagnostics.StageEnd(inferenceStage);
            _forensics.ObjectDisposed("NamedOnnxValue", "recognition input");
            _diagnostics.ObjectCreated("DisposableNamedOnnxValue collection", "recognition outputs");
            _forensics.ObjectCreated("DisposableNamedOnnxValue collection", "recognition outputs");
            var embedding = outputs.First().AsEnumerable<float>().ToArray();
            var normalized = InsightFaceImageMath.L2Normalize(embedding);

            // `outputs` is disposed immediately after this method returns by the `using` above.
            _diagnostics.ObjectDisposed("DisposableNamedOnnxValue collection", "recognition outputs");
            _forensics.ObjectDisposed("DisposableNamedOnnxValue collection", "recognition outputs");
            // AI17.RUNTIME.5: "Inference Session Reused"=true (same cached-once host session as
            // detection above); "Tensor Reused"=true — this is precisely the AI16.RUNTIME.3 pooled
            // DenseTensor<float> backing buffer (rented from ArrayPool<float>.Shared), unlike the
            // detection tensor above.
            _forensics.RecordOnnxInference(
                model: _options.RecognitionModelFile,
                inputTensorShape: $"[{string.Join('x', inputTensor.Dimensions.ToArray())}]",
                outputTensorShape: $"{outputs.Count} tensors",
                inferenceDurationMs: onnxInferenceStopwatch.ElapsedMilliseconds,
                before: beforeEmbeddingInference,
                after: afterEmbeddingInference,
                inferenceSessionReused: true,
                tensorReused: true,
                disposableOutputCount: outputs.Count);
            _memoryAudit.RecordOnnxInference(
                model: _options.RecognitionModelFile,
                inputTensorShape: $"[{string.Join('x', inputTensor.Dimensions.ToArray())}]",
                outputTensorShape: $"{outputs.Count} tensors",
                inputBytesApprox: length * (long)sizeof(float),
                outputBytesApprox: embedding.Length * (long)sizeof(float),
                before: beforeEmbeddingInferenceAudit,
                after: afterEmbeddingInferenceAudit,
                inferenceDurationMs: onnxInferenceStopwatch.ElapsedMilliseconds,
                disposableOutputCount: outputs.Count,
                outputsDisposed: true);
            return normalized;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(rented);
            _diagnostics.ObjectDisposed("DenseTensor<float>", "recognition input (pooled, returned)");
            _forensics.ObjectDisposed("DenseTensor<float>", "recognition input (pooled)");
        }
    }

    /// <summary>
    /// AI18.MEMORY.1 — a raw <see cref="MemoryAuditSnapshot"/> capture for ONNX before/after deltas fed
    /// into <see cref="IRecognitionMemoryAudit.RecordOnnxInference"/>. Trace id/elapsed are left blank
    /// here (this engine has no <c>IRecognitionExecutionContext</c> dependency and none of its recognition
    /// logic needs one) — harmless, since <c>RecordOnnxInference</c> only reads the raw
    /// WorkingSet/NativeEstimate fields from these two snapshots, never their ExecutionTraceId/Elapsed.
    /// Peaks are seeded at 0 for the same reason <see cref="RecognitionMemoryAudit"/>'s own peak state is
    /// only ever advanced by <see cref="IRecognitionMemoryAudit.Snapshot"/>.
    /// </summary>
    private static MemoryAuditSnapshot CaptureRawMemoryAuditSnapshot(string stage) =>
        MemoryAuditSnapshot.Capture(string.Empty, stage, 0, 0, 0, 0, 0);

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
