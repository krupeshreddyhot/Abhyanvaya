using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Application.Recognition.Orchestration;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Recognition pipeline workflow engine (AI20.PHASE2.3).</summary>
public interface IRecognitionPipelineExecutor
{
    Task<RecognitionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default);
}
