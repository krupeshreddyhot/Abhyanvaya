using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically re-runs conflict detection for the current academic year (tenant-agnostic noop without scope context).
/// Detection only — never mutates timetables.
/// </summary>
public sealed class ConflictValidationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConflictValidationBackgroundService> _logger;

    public ConflictValidationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ConflictValidationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay startup so the app can finish bootstrapping.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var detection = scope.ServiceProvider.GetRequiredService<IConflictDetectionService>();

                // Background runs require an ambient tenant; skip quietly when none is established.
                var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                if (currentUser.TenantId <= 0)
                {
                    _logger.LogDebug("Conflict validation background skipped — no tenant context.");
                }
                else
                {
                    await detection.AnalyzeAsync(new RunConflictDetectionRequest
                    {
                        TriggerSource = "Background"
                    }, stoppingToken);
                    _logger.LogInformation("Conflict validation background run completed for tenant {TenantId}.", currentUser.TenantId);
                }

                _ = context; // reserved for future multi-tenant fan-out
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Conflict validation background run failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
