using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Application.Recognition.Orchestration;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Retrieves possible recognition candidates — never ranks (AI20.PHASE2.3).</summary>
public interface IRecognitionCandidateProvider
{
    Task<IReadOnlyList<RecognitionCandidate>> GetCandidatesAsync(
        RecognitionCandidateFilter filter,
        CancellationToken cancellationToken = default);
}
