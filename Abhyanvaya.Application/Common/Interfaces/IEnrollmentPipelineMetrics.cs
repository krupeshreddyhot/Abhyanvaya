namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentPipelineMetrics
{
    void RecordPipelineStarted(Guid correlationId, int pipelineVersion);

    void RecordPipelineCompleted(Guid correlationId, long durationMs, bool success);

    void RecordStageCompleted(string stageName, long durationMs, bool success);

    void RecordStageRetry(string stageName, int attemptCount);

    void RecordPipelineCancelled(Guid correlationId);
}
