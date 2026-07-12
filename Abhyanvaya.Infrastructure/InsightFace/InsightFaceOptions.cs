namespace Abhyanvaya.Infrastructure.InsightFace;

/// <summary>Configuration for InsightFace ONNX models (buffalo_l / det_10g + w600k_r50).</summary>
public sealed class InsightFaceOptions
{
    public const string SectionName = "InsightFace";

    public string ModelDirectory { get; set; } = "models/insightface";

    public string DetectionModelFile { get; set; } = "det_10g.onnx";

    public string RecognitionModelFile { get; set; } = "w600k_r50.onnx";

    public float DetectionThreshold { get; set; } = 0.5f;

    public float NmsThreshold { get; set; } = 0.4f;

    public int DetectionInputSize { get; set; } = 640;

    public int RecognitionInputSize { get; set; } = 112;

    public int ExpectedEmbeddingDimension { get; set; } = 512;

    public string PipelineVersion { get; set; } = "InsightFace-1.0";

    /// <summary>
    /// ONNX Runtime intra-op thread count per session. Defaults to 1 because Render's Starter/Free
    /// instances only grant 0.5 vCPU; letting ONNX Runtime auto-detect logical cores can spin up more
    /// worker threads (and per-thread allocator arenas) than the container can actually use, wasting
    /// memory that is already under pressure from the two resident models. Raise this only on
    /// instances with real spare CPU headroom.
    /// </summary>
    public int IntraOpNumThreads { get; set; } = 1;

    /// <summary>ONNX Runtime inter-op thread count per session. See <see cref="IntraOpNumThreads"/>.</summary>
    public int InterOpNumThreads { get; set; } = 1;

    /// <summary>
    /// ONNX Runtime CPU memory arena (AI16.RUNTIME.1). The arena pre-allocates and retains large
    /// blocks for reuse across inference calls, which lowers allocation overhead but means the
    /// process holds onto that reserved memory even when a session is idle. Defaults to
    /// <see langword="false"/> — Microsoft's own documented recommendation for memory-constrained CPU
    /// inference — since Render's Starter plan (512 MB) is memory-, not CPU-, constrained. Purely an
    /// allocator strategy: identical inference outputs either way.
    /// </summary>
    public bool EnableCpuMemArena { get; set; } = false;

    /// <summary>
    /// ONNX Runtime memory-pattern optimization (AI16.RUNTIME.1): traces allocation shapes from the
    /// first <c>Run()</c> and reuses one pre-planned allocation for subsequent runs with the same
    /// input shape. Paired with <see cref="EnableCpuMemArena"/> in Microsoft's low-memory guidance —
    /// the pattern planner is designed to work with the arena, so the two are disabled together.
    /// Defaults to <see langword="false"/>. Purely an allocator strategy: identical inference outputs
    /// either way.
    /// </summary>
    public bool EnableMemoryPattern { get; set; } = false;
}
