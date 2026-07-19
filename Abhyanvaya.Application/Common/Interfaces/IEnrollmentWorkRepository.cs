using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentWorkRepository
{
    Task<EnrollmentWorkItem?> ClaimNextAsync(CancellationToken cancellationToken = default);

    Task<EnrollmentWorkItem?> GetByItemIdAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid itemId,
        DateTime nextAttemptUtc,
        string? lastError,
        FailureCategory? failureCategory,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentWorkItem>> GetStuckInFlightItemsAsync(
        DateTime staleBeforeUtc,
        int limit,
        CancellationToken cancellationToken = default);

    Task RequeueAsync(Guid itemId, CancellationToken cancellationToken = default);
}
