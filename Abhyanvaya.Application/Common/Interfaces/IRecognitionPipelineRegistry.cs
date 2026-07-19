using Abhyanvaya.Application.Recognition.Pipeline;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Ordered recognition pipeline stage registry (AI20.PHASE2.3).</summary>
public interface IRecognitionPipelineRegistry
{
    IReadOnlyList<IRecognitionPipelineStage> GetOrderedStages(int pipelineVersion);
}
