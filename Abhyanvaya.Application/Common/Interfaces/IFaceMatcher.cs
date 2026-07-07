using Abhyanvaya.Application.DTOs.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Matches detected face embeddings against enrolled student embeddings.
/// Does not create attendance records.
/// </summary>
public interface IFaceMatcher
{
    /// <summary>Human-readable matcher name for diagnostics/UI (e.g. <c>Cosine Similarity</c>).</summary>
    string Name { get; }

    /// <summary>Matcher implementation version, independent of the recognition pipeline version.</summary>
    string Version { get; }

    /// <summary>Underlying matching algorithm (e.g. <c>Cosine Distance</c>).</summary>
    string Algorithm { get; }

    IReadOnlyList<FaceMatchResultDto> Match(
        IReadOnlyList<DetectedFaceMatchInput> detectedFaces,
        IReadOnlyList<StudentEmbeddingMatchInput> studentEmbeddings,
        FaceMatchOptions? options = null);
}
