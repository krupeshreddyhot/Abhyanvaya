using Abhyanvaya.Application.ModelLifecycle;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Observational drift detection — no automatic correction (AI20.PHASE2.5).</summary>
public interface IDriftDetectionService
{
    Task<RecognitionDriftReport> DetectAsync(DriftDetectionRequest request, CancellationToken cancellationToken = default);
}
