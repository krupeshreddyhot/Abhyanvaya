using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Recognition data access — all SQL behind this repository (AI20.PHASE2.3).</summary>
public interface IRecognitionRepository
{
    Task<IReadOnlyList<RecognitionCandidate>> GetActiveEmbeddingsAsync(
        RecognitionCandidateFilter filter,
        CancellationToken cancellationToken = default);

    Task<RecognitionPersistenceResult> PersistRecognitionAsync(
        RecognitionPersistenceRequest request,
        CancellationToken cancellationToken = default);
}
