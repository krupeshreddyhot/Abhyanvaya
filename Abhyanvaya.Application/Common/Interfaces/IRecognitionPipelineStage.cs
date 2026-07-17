using Abhyanvaya.Application.Recognition.Orchestration;
using Abhyanvaya.Application.Recognition.Pipeline;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>One recognition pipeline stage (AI20.PHASE2.3).</summary>
public interface IRecognitionPipelineStage
{
    RecognitionPipelineStage? ManifestStage { get; }

    string Name { get; }

    int Order { get; }

    string Description { get; }

    string Version { get; }

    Task<RecognitionPipelineStageExecutionResult> ExecuteAsync(
        RecognitionPipelineContext context,
        CancellationToken cancellationToken = default);
}
