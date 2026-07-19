using Abhyanvaya.Application.Enrollment.Background;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentSchedulingPolicy
{
    IReadOnlyList<EnrollmentWorkItem> OrderCandidates(IReadOnlyList<EnrollmentWorkItem> candidates);

    bool IsEligible(EnrollmentWorkItem item, DateTime utcNow);
}
