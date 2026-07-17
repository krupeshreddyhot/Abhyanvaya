using Abhyanvaya.Application.Enrollment.Background;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentWorker
{
    Task<EnrollmentWorkerResult?> ProcessNextAsync(CancellationToken cancellationToken = default);
}
