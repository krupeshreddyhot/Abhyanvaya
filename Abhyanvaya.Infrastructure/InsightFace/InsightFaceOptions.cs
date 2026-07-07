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
}
