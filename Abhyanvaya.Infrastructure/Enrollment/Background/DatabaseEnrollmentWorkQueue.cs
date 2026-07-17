using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class DatabaseEnrollmentWorkQueue : IEnrollmentWorkQueue
{
    private readonly IEnrollmentWorkScheduler _scheduler;
    private readonly IEnrollmentJobQueue _wakeSignal;
    private readonly EnrollmentBackgroundOptions _options;

    public DatabaseEnrollmentWorkQueue(
        IEnrollmentWorkScheduler scheduler,
        IEnrollmentJobQueue wakeSignal,
        IOptions<EnrollmentBackgroundOptions> options)
    {
        _scheduler = scheduler;
        _wakeSignal = wakeSignal;
        _options = options.Value;
    }

    public Task SignalAsync(CancellationToken cancellationToken = default)
    {
        _wakeSignal.SignalWork();
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<EnrollmentWorkItem> DequeueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
        using var timer = new PeriodicTimer(pollInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            var work = await _scheduler.GetNextWorkAsync(cancellationToken);
            if (work != null)
            {
                yield return work;
                continue;
            }

            var delayTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();
            var wakeTask = _wakeSignal.WaitForSignalAsync(cancellationToken).AsTask();
            await Task.WhenAny(delayTask, wakeTask);
        }
    }
}
