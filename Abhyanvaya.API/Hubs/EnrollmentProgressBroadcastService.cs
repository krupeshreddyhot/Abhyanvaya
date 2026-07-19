using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.API.Hubs;

/// <summary>
/// Server-side progress broadcaster for active enrollment batches (SignalR push — UI does not poll).
/// </summary>
public sealed class EnrollmentProgressBroadcastService : BackgroundService
{
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnrollmentProgressBroadcastService> _logger;

    public EnrollmentProgressBroadcastService(
        IServiceScopeFactory scopeFactory,
        ILogger<EnrollmentProgressBroadcastService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastActiveBatchesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Enrollment progress broadcast failed.");
            }

            await Task.Delay(BroadcastInterval, stoppingToken);
        }
    }

    private async Task BroadcastActiveBatchesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var history = scope.ServiceProvider.GetRequiredService<IEnrollmentHistoryService>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEnrollmentEventPublisher>();

        var activeBatchIds = await context.StudentEnrollmentBatches
            .AsNoTracking()
            .Where(b => b.Status == BatchStatus.Created || b.Status == BatchStatus.Running)
            .Select(b => new { b.Id, b.TenantId })
            .ToListAsync(cancellationToken);

        foreach (var batch in activeBatchIds)
        {
            var progress = await history.GetBatchProgressAsync(batch.Id, batch.TenantId, cancellationToken);
            if (progress is not null)
            {
                await publisher.PublishBatchProgressAsync(progress, cancellationToken);
            }
        }
    }
}
