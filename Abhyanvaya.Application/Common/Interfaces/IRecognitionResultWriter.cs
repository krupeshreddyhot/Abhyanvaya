using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Persists recognition results and audit metadata — no AI logic (AI20.PHASE2.3).</summary>
public interface IRecognitionResultWriter
{
    Task<RecognitionPersistenceResult> PersistAsync(
        RecognitionPersistenceRequest request,
        CancellationToken cancellationToken = default);
}
