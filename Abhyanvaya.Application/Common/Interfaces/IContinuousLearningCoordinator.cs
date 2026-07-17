using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Queues retraining candidates from teacher corrections — never retrains (AI20.PHASE2.5).</summary>
public interface IContinuousLearningCoordinator
{
    Task<RetrainingCandidate> QueueCandidateAsync(QueueRetrainingCandidateRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrainingCandidate>> ListCandidatesAsync(int tenantId, CancellationToken cancellationToken = default);
}
