namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentWorkerMetrics
{
    void RecordWorkerStarted(string workerId);

    void RecordWorkerCompleted(string workerId, long durationMs, bool success);

    void RecordLeaseAcquired(Guid leaseId);

    void RecordLeaseReleased(Guid leaseId);

    void RecordRecovery(int expiredLeases, int stuckItems, int requeued);
}
