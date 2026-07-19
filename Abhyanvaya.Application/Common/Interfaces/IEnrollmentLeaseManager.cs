using Abhyanvaya.Application.Enrollment.Background;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentLeaseManager
{
    Task<EnrollmentLease?> AcquireAsync(
        EnrollmentWorkItem workItem,
        string workerId,
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<bool> RenewAsync(EnrollmentLease lease, CancellationToken cancellationToken = default);

    Task ReleaseAsync(EnrollmentLease lease, CancellationToken cancellationToken = default);

    Task<int> ExpireAbandonedLeasesAsync(CancellationToken cancellationToken = default);
}
