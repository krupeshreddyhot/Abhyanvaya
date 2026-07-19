using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Candidate filtering strategy — no hardcoded scope logic (AI20.PHASE2.3).</summary>
public interface IRecognitionCandidateStrategy
{
    RecognitionCandidateScope Scope { get; }

    bool CanHandle(RecognitionCandidateFilter filter);

    Task<IReadOnlyList<RecognitionCandidate>> ResolveCandidatesAsync(
        RecognitionCandidateFilter filter,
        IRecognitionRepository repository,
        CancellationToken cancellationToken = default);
}
