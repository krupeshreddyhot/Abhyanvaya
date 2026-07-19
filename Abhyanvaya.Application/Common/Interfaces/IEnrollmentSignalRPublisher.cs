using Abhyanvaya.Application.EnrollmentApi;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentSignalRPublisher
{
    Task PublishBatchCreatedAsync(int tenantId, Guid batchId, int totalStudents, CancellationToken cancellationToken = default);

    Task PublishBatchStartedAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default);

    Task PublishProgressAsync(int tenantId, BatchProgressDto progress, CancellationToken cancellationToken = default);

    Task PublishCompletedAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default);

    Task PublishCancelledAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default);

    Task PublishFailedAsync(int tenantId, Guid batchId, string reason, CancellationToken cancellationToken = default);

    Task PublishDashboardChangedAsync(int tenantId, object dashboardSummary, CancellationToken cancellationToken = default);

    Task PublishRecoveryAsync(int tenantId, object recoveryEvent, CancellationToken cancellationToken = default);
}
