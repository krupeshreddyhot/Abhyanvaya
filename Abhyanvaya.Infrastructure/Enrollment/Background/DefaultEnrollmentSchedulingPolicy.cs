using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class DefaultEnrollmentSchedulingPolicy : IEnrollmentSchedulingPolicy
{
    public bool IsEligible(EnrollmentWorkItem item, DateTime utcNow) =>
        item.Status is EnrollmentStatus.Pending or EnrollmentStatus.RetryRequired
        && (!item.NextAttemptUtc.HasValue || item.NextAttemptUtc.Value <= utcNow);

    public IReadOnlyList<EnrollmentWorkItem> OrderCandidates(IReadOnlyList<EnrollmentWorkItem> candidates) =>
        candidates
            .OrderByDescending(item => item.BatchPriority)
            .ThenBy(item => item.NextAttemptUtc ?? DateTime.MinValue)
            .ThenBy(item => item.RetryCount)
            .ToList();
}
