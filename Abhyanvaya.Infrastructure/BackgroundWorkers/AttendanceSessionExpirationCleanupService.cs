using Abhyanvaya.Application.AttendanceRecovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>AI22.8 — periodic expiration of stale non-finalized attendance sessions.</summary>
public sealed class AttendanceSessionExpirationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttendanceRecoveryOptions _options;
    private readonly ILogger<AttendanceSessionExpirationCleanupService> _logger;

    public AttendanceSessionExpirationCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<AttendanceRecoveryOptions> options,
        ILogger<AttendanceSessionExpirationCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ExpirationCleanupEnabled)
        {
            _logger.LogInformation("AI22.8 attendance expiration cleanup is disabled.");
            return;
        }

        var delay = TimeSpan.FromMinutes(Math.Max(5, _options.CleanupScanIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var expiration = scope.ServiceProvider.GetRequiredService<IAttendanceExpirationService>();
                var count = await expiration.ExpireStaleSessionsAsync(stoppingToken);
                if (count > 0)
                    _logger.LogInformation("AI22.8 expiration cleanup marked {Count} sessions expired.", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "AI22.8 expiration cleanup failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
