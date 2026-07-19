using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Collects operational recognition metrics (AI20.PHASE2.5).</summary>
public interface IRecognitionMetricsService
{
    Task<RecognitionMetricsSnapshot> GetSnapshotAsync(Guid? modelId = null, CancellationToken cancellationToken = default);
}
