using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>
/// Processes <see cref="StudentPhotoUploadedMessage"/> jobs via <see cref="IEmbeddingPipeline"/>.
/// </summary>
/// <remarks>
/// Uses an in-memory queue today. The queue abstraction can be replaced with Hangfire or Quartz
/// without changing the embedding pipeline.
/// </remarks>
public sealed class StudentFaceEmbeddingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStudentPhotoEmbeddingQueue _queue;
    private readonly ILogger<StudentFaceEmbeddingBackgroundService> _logger;

    public StudentFaceEmbeddingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IStudentPhotoEmbeddingQueue queue,
        ILogger<StudentFaceEmbeddingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Student face embedding background worker started.");

        await foreach (var message in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation(
                    "Face embedding job dequeued. StudentId={StudentId} TenantId={TenantId} QueueDepth={QueueDepth}",
                    message.StudentId,
                    message.TenantId,
                    _queue.Count);

                await ProcessMessageAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Face embedding job failed. StudentId={StudentId} TenantId={TenantId}",
                    message.StudentId,
                    message.TenantId);

                _queue.MarkCompleted(message.StudentId);
            }
        }
    }

    private async Task ProcessMessageAsync(StudentPhotoUploadedMessage message, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        // Establish the ambient tenant for this scope so EF Core global query filters resolve
        // exactly as they do for an authenticated HTTP request. The pipeline stays tenant-unaware.
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant(message.TenantId);

        try
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IEmbeddingPipeline>();
            await pipeline.GenerateAsync(message, cancellationToken);
        }
        finally
        {
            // Always clear the tenant so no state leaks across jobs, even on failure.
            tenantContext.Clear();
        }
    }
}
