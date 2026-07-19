using Abhyanvaya.Application.Enrollment.Background;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentRecoveryService
{
    Task<EnrollmentRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default);
}
