using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentBackgroundService : BackgroundService, IEnrollmentBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EnrollmentBackgroundOptions _options;
    private readonly ILogger<EnrollmentBackgroundService> _logger;
    private volatile bool _isRunning;

    public EnrollmentBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<EnrollmentBackgroundOptions> options,
        ILogger<EnrollmentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsRunning => _isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Enrollment background processing is disabled.");
            return;
        }

        _isRunning = true;
        _logger.LogInformation("Enrollment background service started.");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var workerHost = scope.ServiceProvider.GetRequiredService<IEnrollmentWorkerHost>();
            await workerHost.RunAsync(stoppingToken);
        }
        finally
        {
            _isRunning = false;
            _logger.LogInformation("Enrollment background service stopped.");
        }
    }
}
