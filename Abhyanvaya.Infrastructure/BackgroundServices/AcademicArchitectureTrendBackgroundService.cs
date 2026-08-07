using Abhyanvaya.Application.Academic.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.BackgroundServices;

/// <summary>
/// AI29.1A.7 — Periodically captures architecture trends asynchronously.
/// Never blocks user requests; advisory observability only.
/// </summary>
public sealed class AcademicArchitectureTrendBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AcademicPlatformOptions> _options;
    private readonly ILogger<AcademicArchitectureTrendBackgroundService> _logger;

    public AcademicArchitectureTrendBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AcademicPlatformOptions> options,
        ILogger<AcademicArchitectureTrendBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay first run so startup is not blocked.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Value.EnableArchitectureMetrics)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var trends = scope.ServiceProvider.GetRequiredService<IAcademicArchitectureTrendService>();
                    await trends.CaptureAsync(stoppingToken);
                    _logger.LogInformation("Academic architecture trend capture completed");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Academic architecture trend capture failed (advisory only)");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
