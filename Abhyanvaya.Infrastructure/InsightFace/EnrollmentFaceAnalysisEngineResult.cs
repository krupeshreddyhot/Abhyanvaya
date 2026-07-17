namespace Abhyanvaya.Infrastructure.InsightFace;

/// <summary>Internal engine result for enrollment validation (Infrastructure-only).</summary>
public sealed record EnrollmentFaceAnalysisEngineResult(
    int ImageWidth,
    int ImageHeight,
    IReadOnlyList<EnrollmentFaceAnalysisEngineFace> Faces,
    byte[]? AlignedFaceWebpBytes);

public sealed record EnrollmentFaceAnalysisEngineFace(
    float DetectionScore,
    int BoundingBoxX,
    int BoundingBoxY,
    int BoundingBoxWidth,
    int BoundingBoxHeight,
    float[] Landmarks);
