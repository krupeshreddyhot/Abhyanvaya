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

            session = new InferenceSession(path);
            _logger.LogInformation("InsightFace {Label} ONNX model loaded from {Path}", label, path);
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
