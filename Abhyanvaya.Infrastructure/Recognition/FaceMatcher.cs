using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Recognition;

/// <summary>Cosine-distance face matcher for normalized embedding vectors.</summary>
public sealed class FaceMatcher : IFaceMatcher
{
    private readonly InsightFace.InsightFaceOptions _options;

    public FaceMatcher(IOptions<InsightFace.InsightFaceOptions> options)
    {
        _options = options.Value;
    }

    public string Name => "Cosine Similarity";

    public string Version => "1.0";

    public string Algorithm => "Cosine Distance";

    public IReadOnlyList<FaceMatchResultDto> Match(
        IReadOnlyList<DetectedFaceMatchInput> detectedFaces,
        IReadOnlyList<StudentEmbeddingMatchInput> studentEmbeddings,
        FaceMatchOptions? options = null)
    {
        options ??= new FaceMatchOptions();
        var results = new List<FaceMatchResultDto>();
        var assignedStudents = new HashSet<int>();

        foreach (var face in detectedFaces)
        {
            var best = FindBestMatch(face.Embedding, studentEmbeddings);
            var result = new FaceMatchResultDto { FaceIndex = face.FaceIndex };

            if (best == null)
            {
                result.SuggestedStatus = RecognitionStatus.Unknown;
                result.Confidence = 0;
                result.Distance = 1;
            }
            else
            {
                result.MatchedStudentId = best.Value.StudentId;
                result.MatchedEmbeddingId = best.Value.EmbeddingId;
                result.Distance = (decimal)best.Value.Distance;
                result.Confidence = (decimal)Math.Clamp((1f - best.Value.Distance) * 100f, 0f, 100f);

                if (best.Value.Distance <= options.MatchDistanceThreshold)
                {
                    result.SuggestedStatus = assignedStudents.Contains(best.Value.StudentId)
                        ? RecognitionStatus.Duplicate
                        : RecognitionStatus.Recognized;
                }
                else if (best.Value.Distance <= options.LowConfidenceDistanceThreshold)
                {
                    result.SuggestedStatus = RecognitionStatus.LowConfidence;
                }
                else
                {
                    result.SuggestedStatus = RecognitionStatus.Unknown;
                    result.MatchedStudentId = null;
                    result.MatchedEmbeddingId = null;
                }

                if (result.SuggestedStatus == RecognitionStatus.Recognized && result.MatchedStudentId.HasValue)
                {
                    assignedStudents.Add(result.MatchedStudentId.Value);
                }
            }

            results.Add(result);
        }

        return results;
    }

    private static (int StudentId, Guid EmbeddingId, float Distance)? FindBestMatch(
        float[] query,
        IReadOnlyList<StudentEmbeddingMatchInput> students)
    {
        (int StudentId, Guid EmbeddingId, float Distance)? best = null;

        foreach (var student in students)
        {
            if (student.Embedding.Length == 0 || student.Embedding.Length != query.Length)
            {
                continue;
            }

            var distance = CosineDistance(query, student.Embedding);
            if (best == null || distance < best.Value.Distance)
            {
                best = (student.StudentId, student.EmbeddingId, distance);
            }
        }

        return best;
    }

    private static float CosineDistance(float[] a, float[] b)
    {
        var dot = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return 1f - Math.Clamp(dot, -1f, 1f);
    }
}
