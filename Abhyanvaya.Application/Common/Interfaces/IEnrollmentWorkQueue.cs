using Abhyanvaya.Application.Enrollment.Background;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentWorkQueue
{
    Task SignalAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<EnrollmentWorkItem> DequeueAsync(CancellationToken cancellationToken);
}
