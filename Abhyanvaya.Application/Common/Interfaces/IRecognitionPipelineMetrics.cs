namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Recognition pipeline telemetry (AI20.PHASE2.3).</summary>
public interface IRecognitionPipelineMetrics
{
    void RecordPipelineStarted(Guid correlationId, int pipelineVersion);

    void RecordPipelineCompleted(Guid correlationId, bool success, TimeSpan duration);

    void RecordStageCompleted(Guid correlationId, string stageName, TimeSpan duration, bool success);
}
