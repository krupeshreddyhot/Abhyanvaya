using Abhyanvaya.Application.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

public sealed class TimetableCloneBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TimetableCloneBackgroundService> _logger;

    public TimetableCloneBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TimetableCloneBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Timetable clone background service started.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            do
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var cloneService = scope.ServiceProvider.GetRequiredService<ITimetableCloneService>();
                    var jobs = await cloneService.ListAsync(Domain.Enums.Scheduling.TimetableCloneJobStatus.Queued, stoppingToken);
                    foreach (var job in jobs)
                    {
                        try
                        {
                            await cloneService.ExecuteJobAsync(job.Id, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Timetable clone job {JobId} failed.", job.Id);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Timetable clone background sweep failed.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }

        _logger.LogInformation("Timetable clone background service stopped.");
    }
}
