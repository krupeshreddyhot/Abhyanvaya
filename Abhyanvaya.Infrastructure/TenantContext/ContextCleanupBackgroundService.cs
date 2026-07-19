using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class ContextCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ContextPlatformOptions> _options;
    private readonly ILogger<ContextCleanupBackgroundService> _logger;

    public ContextCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ContextPlatformOptions> options,
        ILogger<ContextCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupSweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Context cleanup sweep failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.Value.CleanupIntervalMinutes), stoppingToken);
        }
    }

    private async Task RunCleanupSweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var worker = scope.ServiceProvider.GetRequiredService<IContextCleanupWorker>();
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

        if (currentUser.UserId > 0)
        {
            var cleaned = await worker.CleanupExpiredContextAsync(currentUser.UserId, cancellationToken);
            if (cleaned > 0)
            {
                _logger.LogInformation("Expired operational context cleaned for UserId={UserId}", currentUser.UserId);
            }
        }
    }
}
