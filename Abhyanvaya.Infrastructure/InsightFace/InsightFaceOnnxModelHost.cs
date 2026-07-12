using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;

namespace Abhyanvaya.Infrastructure.InsightFace;

/// <summary>Lazy-loaded ONNX inference sessions for InsightFace detection and recognition models.</summary>
public sealed class InsightFaceOnnxModelHost : IDisposable
{
    private readonly InsightFaceOptions _options;
    private readonly ILogger<InsightFaceOnnxModelHost> _logger;
    private readonly object _gate = new();
    private InferenceSession? _detectionSession;
    private InferenceSession? _recognitionSession;

    public InsightFaceOnnxModelHost(IOptions<InsightFaceOptions> options, ILogger<InsightFaceOnnxModelHost> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public InferenceSession GetDetectionSession()
    {
        EnsureLoaded(ref _detectionSession, _options.DetectionModelFile, "detection");
        return _detectionSession!;
    }

    public InferenceSession GetRecognitionSession()
    {
        EnsureLoaded(ref _recognitionSession, _options.RecognitionModelFile, "recognition");
        return _recognitionSession!;
    }

    private void EnsureLoaded(ref InferenceSession? session, string modelFile, string label)
    {
        if (session != null)
        {
            return;
        }

        lock (_gate)
        {
            if (session != null)
            {
                return;
            }

            var path = Path.Combine(_options.ModelDirectory, modelFile);
            EnsureModelFilePresent(path, label);

            // Explicit, bounded thread counts (see InsightFaceOptions.IntraOpNumThreads) instead of
            // ONNX Runtime's default of "one thread per detected logical core" — on memory-constrained
            // hosts (e.g. Render Starter: 512 MB / 0.5 vCPU) unconstrained thread/arena allocation has
            // contributed to the process exceeding its memory limit under load.
            //
            // AI16.RUNTIME.1: EnableCpuMemArena/EnableMemoryPattern are also explicitly set (rather
            // than left at their ORT defaults of `true`/`true`) per Microsoft's documented low-memory
            // CPU inference guidance. See docs/AI16_RUNTIME1_ONNX_MEMORY_OPTIMIZATION.md — both are
            // pure allocator-strategy switches with no effect on inference math, so detection/embedding
            // outputs are byte-for-byte identical regardless of these settings.
            var sessionOptions = new SessionOptions
            {
                IntraOpNumThreads = _options.IntraOpNumThreads,
                InterOpNumThreads = _options.InterOpNumThreads,
                EnableCpuMemArena = _options.EnableCpuMemArena,
                EnableMemoryPattern = _options.EnableMemoryPattern,
            };

            session = new InferenceSession(path, sessionOptions);
            _logger.LogInformation(
                "InsightFace {Label} ONNX model loaded from {Path} (IntraOpNumThreads={IntraOpNumThreads}, InterOpNumThreads={InterOpNumThreads}, EnableCpuMemArena={EnableCpuMemArena}, EnableMemoryPattern={EnableMemoryPattern})",
                label,
                path,
                _options.IntraOpNumThreads,
                _options.InterOpNumThreads,
                _options.EnableCpuMemArena,
                _options.EnableMemoryPattern);
        }
    }

    public void Dispose()
    {
        _detectionSession?.Dispose();
        _recognitionSession?.Dispose();
    }

    private static void EnsureModelFilePresent(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"InsightFace {label} model not found at '{path}'. " +
                "The API (not the Cloudflare Pages UI) must be deployed with ONNX files under " +
                "Abhyanvaya.API/models/insightface/. Run 'git lfs pull' before publish if models are stored in Git LFS.",
                path);
        }

        // Git LFS pointer files are ~130 bytes and start with this header — they are not valid ONNX.
        if (new FileInfo(path).Length < 4096 &&
            File.ReadLines(path).FirstOrDefault()?.StartsWith("version https://git-lfs.github.com", StringComparison.Ordinal) == true)
        {
            throw new FileNotFoundException(
                $"InsightFace {label} model at '{path}' is a Git LFS pointer, not the actual ONNX file. " +
                "Run 'git lfs pull' on the API deployment machine (or CI checkout with LFS enabled) and redeploy the API.",
                path);
        }
    }
}
