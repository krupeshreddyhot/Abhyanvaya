using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>Processes classroom photo upload jobs via <see cref="IClassroomRecognitionPipeline"/>.</summary>
public sealed class ClassroomRecognitionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClassroomPhotoQueue _queue;
    private readonly ILogger<ClassroomRecognitionBackgroundService> _logger;

    public ClassroomRecognitionBackgroundService(
        IServiceScopeFactory scopeFactory,
        IClassroomPhotoQueue queue,
        ILogger<ClassroomRecognitionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Classroom recognition background worker started.");

        await foreach (var message in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation(
                    "Classroom recognition job dequeued. SessionId={SessionId} TenantId={TenantId} QueueDepth={QueueDepth}",
                    message.AttendanceSessionId,
                    message.TenantId,
                    _queue.Count);

                await using var scope = _scopeFactory.CreateAsyncScope();

                // Establish the ambient tenant for this scope so EF Core global query filters
                // resolve exactly as they do for an authenticated HTTP request. The pipeline stays
                // unaware of tenancy; it simply queries and the filters apply the correct tenant.
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
                tenantContext.SetTenant(message.TenantId);

                try
                {
                    var pipeline = scope.ServiceProvider.GetRequiredService<IClassroomRecognitionPipeline>();
                    await pipeline.ProcessAsync(message, stoppingToken);
                }
                finally
                {
                    // Always clear the tenant so no state leaks across jobs, even on failure.
                    tenantContext.Clear();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Classroom recognition job failed. SessionId={SessionId} TenantId={TenantId}",
                    message.AttendanceSessionId,
                    message.TenantId);
            }
        }
    }
}
