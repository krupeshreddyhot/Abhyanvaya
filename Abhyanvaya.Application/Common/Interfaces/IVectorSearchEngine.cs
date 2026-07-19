using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Vector search — returns Top-K candidates, never decides (AI20.PHASE2.3).</summary>
public interface IVectorSearchEngine
{
    Task<VectorSearchResponse> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}
