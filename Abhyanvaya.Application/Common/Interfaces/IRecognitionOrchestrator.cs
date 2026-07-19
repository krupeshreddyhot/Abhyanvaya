using Abhyanvaya.Application.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Coordinates recognition workflow — sequence only, no AI logic (AI20.PHASE2.3).</summary>
public interface IRecognitionOrchestrator
{
    Task<RecognitionResult> RecognizeAsync(
        RecognitionPipelineRequest request,
        CancellationToken cancellationToken = default);
}
