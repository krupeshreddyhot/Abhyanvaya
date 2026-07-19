using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

public sealed class VectorSearchEngine : IVectorSearchEngine
{
    private readonly IVectorDatabaseProvider _databaseProvider;

    public VectorSearchEngine(IVectorDatabaseProvider databaseProvider)
    {
        _databaseProvider = databaseProvider;
    }

    public Task<VectorSearchResponse> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken = default) =>
        _databaseProvider.SearchAsync(request, cancellationToken);
}
