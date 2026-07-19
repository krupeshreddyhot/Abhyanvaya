using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Restores previous model versions with audit — no inference (AI20.PHASE2.5).</summary>
public interface IModelRollbackManager
{
    Task<RollbackResult> RollbackAsync(RollbackRequest request, CancellationToken cancellationToken = default);
}
