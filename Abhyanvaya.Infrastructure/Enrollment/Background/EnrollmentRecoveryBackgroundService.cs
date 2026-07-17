using System.Diagnostics;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentRecoveryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EnrollmentRecoveryOptions _options;
    private readonly ILogger<EnrollmentRecoveryBackgroundService> _logger;

    public EnrollmentRecoveryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<EnrollmentRecoveryOptions> options,
        ILogger<EnrollmentRecoveryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Enrollment recovery service is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.ScanIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var recoveryService = scope.ServiceProvider.GetRequiredService<IEnrollmentRecoveryService>();
                await recoveryService.RecoverAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Enrollment recovery sweep failed unexpectedly.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public sealed class EnrollmentRecoveryService : IEnrollmentRecoveryService
{
    private readonly IEnrollmentLeaseManager _leaseManager;
    private readonly IEnrollmentWorkRepository _workRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnrollmentWorkerMetrics _metrics;
    private readonly EnrollmentRecoveryOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentRecoveryService> _logger;

    public EnrollmentRecoveryService(
        IEnrollmentLeaseManager leaseManager,
        IEnrollmentWorkRepository workRepository,
        IUnitOfWork unitOfWork,
        IEnrollmentWorkerMetrics metrics,
        IOptions<EnrollmentRecoveryOptions> options,
        TimeProvider clock,
        ILogger<EnrollmentRecoveryService> logger)
    {
        _leaseManager = leaseManager;
        _workRepository = workRepository;
        _unitOfWork = unitOfWork;
        _metrics = metrics;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EnrollmentRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var expiredLeases = await _leaseManager.ExpireAbandonedLeasesAsync(cancellationToken);

        var cutoff = _clock.GetUtcNow().UtcDateTime.AddMinutes(-Math.Max(1, _options.TimeoutMinutes));
        var stuckItems = await _workRepository.GetStuckInFlightItemsAsync(
            cutoff,
            Math.Max(1, _options.MaxRecoveriesPerRun),
            cancellationToken);

        var requeued = 0;
        foreach (var item in stuckItems)
        {
            if (item.RetryCount >= _options.MaxRetryCount)
            {
                continue;
            }

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _workRepository.RequeueAsync(item.ItemId, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);

            requeued++;
        }

        stopwatch.Stop();
        _metrics.RecordRecovery(expiredLeases, stuckItems.Count, requeued);
        _ = new RecoveryExecuted(expiredLeases, stuckItems.Count, requeued, stopwatch.ElapsedMilliseconds);

        if (expiredLeases > 0 || requeued > 0)
        {
            _logger.LogWarning(
                "Enrollment recovery executed. ExpiredLeases={ExpiredLeases} StuckItems={StuckItems} Requeued={Requeued} DurationMs={DurationMs}",
                expiredLeases,
                stuckItems.Count,
                requeued,
                stopwatch.ElapsedMilliseconds);
        }

        return new EnrollmentRecoveryResult
        {
            ExpiredLeasesRecovered = expiredLeases,
            StuckItemsRecovered = stuckItems.Count,
            RequeuedItems = requeued,
            Duration = stopwatch.Elapsed,
        };
    }
}
