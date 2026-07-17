using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Manages model rollout plans — no inference (AI20.PHASE2.5).</summary>
public interface IModelRolloutManager
{
    Task<RolloutResult> StartRolloutAsync(RolloutRequest request, CancellationToken cancellationToken = default);
}
