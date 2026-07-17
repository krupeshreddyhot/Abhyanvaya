namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentWorkerHost
{
    Task RunAsync(CancellationToken cancellationToken = default);

    int ActiveWorkerCount { get; }
}
