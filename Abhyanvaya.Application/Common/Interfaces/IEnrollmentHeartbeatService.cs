using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentHeartbeatService
{
    Task UpdateAsync(
        EnrollmentLease lease,
        EnrollmentWorkerState pipelineState,
        CancellationToken cancellationToken = default);
}
