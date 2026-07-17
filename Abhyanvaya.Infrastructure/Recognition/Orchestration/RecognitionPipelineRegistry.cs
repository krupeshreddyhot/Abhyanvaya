using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition.Pipeline;

namespace Abhyanvaya.Infrastructure.Recognition.Orchestration;

public sealed class RecognitionPipelineRegistry : IRecognitionPipelineRegistry
{
    private readonly IEnumerable<IRecognitionPipelineStage> _stages;

    public RecognitionPipelineRegistry(IEnumerable<IRecognitionPipelineStage> stages)
    {
        _stages = stages;
    }

    public IReadOnlyList<IRecognitionPipelineStage> GetOrderedStages(int pipelineVersion) =>
        _stages.OrderBy(s => s.Order).ToList();
}
