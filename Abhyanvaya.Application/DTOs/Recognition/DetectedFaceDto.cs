namespace Abhyanvaya.Application.DTOs.Recognition;

/// <summary>A single detected and aligned face with embedding vector.</summary>
public sealed class DetectedFaceDto
{
    public int FaceIndex { get; set; }

    public float DetectionScore { get; set; }

    public int BoundingBoxX { get; set; }

    public int BoundingBoxY { get; set; }

    public int BoundingBoxWidth { get; set; }

    public int BoundingBoxHeight { get; set; }

    public float[] Landmarks { get; set; } = [];

    public float[] Embedding { get; set; } = [];

    public int EmbeddingDimension { get; set; }

    public byte[]? AlignedFaceBytes { get; set; }
}
