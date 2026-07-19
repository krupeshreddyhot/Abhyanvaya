using Abhyanvaya.Application.Enrollment.Background;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentDeadLetterService
{
    Task PersistAsync(EnrollmentDeadLetterRequest request, CancellationToken cancellationToken = default);
}
