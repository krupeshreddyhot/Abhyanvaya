using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentWorkerHost : IEnrollmentWorkerHost
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEnrollmentJobQueue _wakeSignal;
    private readonly EnrollmentBackgroundOptions _options;
    private readonly ILogger<EnrollmentWorkerHost> _logger;
    private int _activeWorkers;

    public EnrollmentWorkerHost(
        IServiceScopeFactory scopeFactory,
        IEnrollmentJobQueue wakeSignal,
        IOptions<EnrollmentBackgroundOptions> options,
        ILogger<EnrollmentWorkerHost> logger)
    {
        _scopeFactory = scopeFactory;
        _wakeSignal = wakeSignal;
        _options = options.Value;
        _logger = logger;
    }

    public int ActiveWorkerCount => Volatile.Read(ref _activeWorkers);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var workerCount = Math.Max(1, _options.WorkerCount);
        _logger.LogInformation("Enrollment worker host starting. WorkerCount={WorkerCount}", workerCount);

        var workers = Enumerable.Range(0, workerCount)
            .Select(index => RunWorkerLoopAsync(index, cancellationToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task RunWorkerLoopAsync(int workerIndex, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeWorkers);
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
        using var timer = new PeriodicTimer(pollInterval);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var worker = scope.ServiceProvider.GetRequiredService<EnrollmentProcessingWorker>();

                    var result = await worker.ProcessNextAsync(cancellationToken);
                    if (result != null)
                    {
                        continue;
                    }

                    var delayTask = timer.WaitForNextTickAsync(cancellationToken).AsTask();
                    var wakeTask = _wakeSignal.WaitForSignalAsync(cancellationToken).AsTask();
                    await Task.WhenAny(delayTask, wakeTask);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Enrollment worker loop failed unexpectedly. WorkerIndex={WorkerIndex}",
                        workerIndex);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeWorkers);
            _logger.LogInformation("Enrollment worker stopped. WorkerIndex={WorkerIndex}", workerIndex);
        }
    }
}
