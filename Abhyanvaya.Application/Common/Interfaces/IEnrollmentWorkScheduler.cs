using Abhyanvaya.Application.Enrollment.Background;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentWorkScheduler
{
    Task<EnrollmentWorkItem?> GetNextWorkAsync(CancellationToken cancellationToken = default);

    Task<EnrollmentRetryScheduleResult> ScheduleRetryAsync(
        EnrollmentRetryScheduleRequest request,
        CancellationToken cancellationToken = default);
}
