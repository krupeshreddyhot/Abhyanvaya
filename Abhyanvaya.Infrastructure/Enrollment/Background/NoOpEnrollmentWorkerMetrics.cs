using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class NoOpEnrollmentWorkerMetrics : IEnrollmentWorkerMetrics
{
    public void RecordWorkerStarted(string workerId)
    {
    }

    public void RecordWorkerCompleted(string workerId, long durationMs, bool success)
    {
    }

    public void RecordLeaseAcquired(Guid leaseId)
    {
    }

    public void RecordLeaseReleased(Guid leaseId)
    {
    }

    public void RecordRecovery(int expiredLeases, int stuckItems, int requeued)
    {
    }
}
