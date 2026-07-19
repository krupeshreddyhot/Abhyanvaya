namespace Abhyanvaya.Infrastructure.Recognition.Orchestration;

public sealed class NoOpRecognitionPipelineMetrics : Application.Common.Interfaces.IRecognitionPipelineMetrics
{
    public void RecordPipelineCompleted(Guid correlationId, bool success, TimeSpan duration)
    {
    }

    public void RecordPipelineStarted(Guid correlationId, int pipelineVersion)
    {
    }

    public void RecordStageCompleted(Guid correlationId, string stageName, TimeSpan duration, bool success)
    {
    }
}
