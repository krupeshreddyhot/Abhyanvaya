namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Storage observability seam (AI20.PHASE2.1.5B). No direct dependency on DataDog/OpenTelemetry/Prometheus.
/// </summary>
public interface IStorageMetricsCollector
{
    void RecordUpload(long elapsedMs, long bytes, string provider, bool success);

    void RecordDownload(long elapsedMs, long bytes, string provider, bool success);

    void RecordThroughput(long bytesPerSecond, string operation);

    void RecordRetry(string operation, int retryCount);

    void RecordProviderLatency(long elapsedMs, string provider, string operation);

    void RecordStorageSize(long bytes, string artifactType);

    void RecordFailure(string operation, string reason);

    void RecordChecksumTime(long elapsedMs);

    void RecordPipelineTime(long elapsedMs, string pipelineName);
}
