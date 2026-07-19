using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration;

public sealed class NoOpEnrollmentPipelineMetrics : IEnrollmentPipelineMetrics
{
    public void RecordPipelineStarted(Guid correlationId, int pipelineVersion)
    {
    }

    public void RecordPipelineCompleted(Guid correlationId, long durationMs, bool success)
    {
    }

    public void RecordStageCompleted(string stageName, long durationMs, bool success)
    {
    }

    public void RecordStageRetry(string stageName, int attemptCount)
    {
    }

    public void RecordPipelineCancelled(Guid correlationId)
    {
    }
}
