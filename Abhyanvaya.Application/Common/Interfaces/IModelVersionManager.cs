using Abhyanvaya.Application.ModelLifecycle;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Creates, activates, and retires model versions — no recognition (AI20.PHASE2.5).</summary>
public interface IModelVersionManager
{
    Task<AIModelDescriptor> CreateVersionAsync(CreateModelVersionRequest request, CancellationToken cancellationToken = default);

    Task<AIModelDescriptor> ActivateVersionAsync(Guid modelVersionId, AIModelState targetState, CancellationToken cancellationToken = default);

    Task<AIModelDescriptor> RetireVersionAsync(Guid modelVersionId, string reason, CancellationToken cancellationToken = default);

    Task<AIModelDescriptor> DeprecateVersionAsync(Guid modelVersionId, CancellationToken cancellationToken = default);
}
