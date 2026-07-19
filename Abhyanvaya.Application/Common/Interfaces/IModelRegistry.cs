using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Registers and tracks AI model metadata — no inference (AI20.PHASE2.5).</summary>
public interface IModelRegistry
{
    Task<AIModelDescriptor> RegisterModelAsync(RegisterModelRequest request, CancellationToken cancellationToken = default);

    Task<AIModelDescriptor?> GetModelAsync(Guid modelId, string version, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AIModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken = default);
}
