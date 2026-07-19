using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

internal sealed class NoOpStorageMetricsCollector : IStorageMetricsCollector
{
    public void RecordUpload(long elapsedMs, long bytes, string provider, bool success)
    {
    }

    public void RecordDownload(long elapsedMs, long bytes, string provider, bool success)
    {
    }

    public void RecordThroughput(long bytesPerSecond, string operation)
    {
    }

    public void RecordRetry(string operation, int retryCount)
    {
    }

    public void RecordProviderLatency(long elapsedMs, string provider, string operation)
    {
    }

    public void RecordStorageSize(long bytes, string artifactType)
    {
    }

    public void RecordFailure(string operation, string reason)
    {
    }

    public void RecordChecksumTime(long elapsedMs)
    {
    }

    public void RecordPipelineTime(long elapsedMs, string pipelineName)
    {
    }
}
