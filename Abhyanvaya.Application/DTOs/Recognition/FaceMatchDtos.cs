using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.Recognition;

public sealed record DetectedFaceMatchInput(
    int FaceIndex,
    float[] Embedding);

public sealed record StudentEmbeddingMatchInput(
    int StudentId,
    Guid EmbeddingId,
    float[] Embedding,
    long PhotoVersion);

public sealed class FaceMatchOptions
{
    public float MatchDistanceThreshold { get; set; } = 0.45f;

    public float LowConfidenceDistanceThreshold { get; set; } = 0.55f;
}

public sealed class FaceMatchResultDto
{
    public int FaceIndex { get; set; }

    public int? MatchedStudentId { get; set; }

    public Guid? MatchedEmbeddingId { get; set; }

    public RecognitionStatus SuggestedStatus { get; set; }

    public decimal Confidence { get; set; }

    public decimal Distance { get; set; }
}
