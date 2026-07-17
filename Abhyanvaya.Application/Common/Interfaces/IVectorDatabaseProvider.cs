using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Vector database abstraction — pgvector now, FAISS/Qdrant/Milvus future (AI20.PHASE2.3).</summary>
public interface IVectorDatabaseProvider
{
    string ProviderName { get; }

    Task<VectorSearchResponse> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default);
}
