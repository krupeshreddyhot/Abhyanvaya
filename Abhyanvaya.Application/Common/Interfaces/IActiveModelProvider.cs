using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Supplies the active production model to consumers — recognition never selects models (AI20.PHASE2.5).</summary>
public interface IActiveModelProvider
{
    Task<AIModelDescriptor?> GetActiveModelAsync(CancellationToken cancellationToken = default);

    Task<AIModelDescriptor?> GetActiveModelForTenantAsync(int tenantId, CancellationToken cancellationToken = default);
}
