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
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"InsightFace {label} model not found at '{path}'. " +
                    "Place ONNX models under InsightFace:ModelDirectory and configure appsettings.",
                    path);
            }

            session = new InferenceSession(path);
            _logger.LogInformation("InsightFace {Label} ONNX model loaded from {Path}", label, path);
        }
    }

    public void Dispose()
    {
        _detectionSession?.Dispose();
        _recognitionSession?.Dispose();
    }
}
