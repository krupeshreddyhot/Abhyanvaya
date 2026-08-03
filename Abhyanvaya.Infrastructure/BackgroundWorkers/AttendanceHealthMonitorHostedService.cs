using Abhyanvaya.Application.AttendanceRecovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.BackgroundWorkers;

/// <summary>
/// AI22.8.5.7 — periodic health scan. Publishes admin SignalR alerts only. Never auto-cancels.
/// </summary>
public sealed class AttendanceHealthMonitorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AttendanceRecoveryOptions> _options;
    private readonly ILogger<AttendanceHealthMonitorHostedService> _logger;

    public AttendanceHealthMonitorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AttendanceRecoveryOptions> options,
        ILogger<AttendanceHealthMonitorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(5, _options.Value.CleanupScanIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<IAttendanceHealthMonitorService>();
                var snapshot = await monitor.ScanAsync(stoppingToken);
                _logger.LogInformation(
                    "AI22.8.5 health monitor: alerts={Alerts} stalledRecognition={Rec} stalledReview={Rev} (never auto-cancels={Never})",
                    snapshot.Alerts.Count,
                    snapshot.RecognitionStalled,
                    snapshot.ReviewStalled,
                    snapshot.NeverAutoCancels);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI22.8.5 health monitor scan failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
