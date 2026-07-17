using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Manages versioned golden datasets — no evaluation (AI20.PHASE2.5).</summary>
public interface IGoldenDatasetManager
{
    Task<GoldenDatasetDescriptor> CreateDatasetAsync(GoldenDatasetDescriptor dataset, CancellationToken cancellationToken = default);

    Task<GoldenDatasetDescriptor?> GetDatasetAsync(Guid datasetId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoldenDatasetDescriptor>> ListVersionsAsync(string datasetKey, CancellationToken cancellationToken = default);
}
