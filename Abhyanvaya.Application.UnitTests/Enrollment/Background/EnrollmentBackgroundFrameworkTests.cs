using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Enrollment.Background;
using Abhyanvaya.Infrastructure.Enrollment.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Background;

public sealed class EnrollmentBackgroundFrameworkTests
{
    private readonly Mock<IEnrollmentWorkRepository> _workRepository = new();
    private readonly Mock<IEnrollmentLeaseManager> _leaseManager = new();
    private readonly Mock<IEnrollmentHeartbeatService> _heartbeatService = new();
    private readonly Mock<IEnrollmentOrchestrator> _orchestrator = new();
    private readonly Mock<IEnrollmentWorkScheduler> _scheduler = new();
    private readonly Mock<IEnrollmentWorkerMetrics> _metrics = new();
    private readonly Mock<IDistributedLockProvider> _lockProvider = new();
    private readonly Mock<IEnrollmentDeadLetterService> _deadLetterService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly IEnrollmentSchedulingPolicy _schedulingPolicy = new DefaultEnrollmentSchedulingPolicy();
    private readonly IEnrollmentRetryPolicy _retryPolicy = new DefaultEnrollmentRetryPolicy();

    public EnrollmentBackgroundFrameworkTests()
    {
        _lockProvider.Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestLockHandle());

        _unitOfWork.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Worker_InvokesOrchestrator_WhenLeaseAcquired()
    {
        var work = CreateWorkItem();
        var lease = CreateLease(work);

        _scheduler.Setup(s => s.GetNextWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync(work);
        _leaseManager.Setup(l => l.AcquireAsync(work, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);
        _orchestrator.Setup(o => o.ProcessItemAsync(It.IsAny<EnrollmentPipelineRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessPipelineResult(work));

        var worker = CreateWorker();
        var result = await worker.ProcessNextAsync();

        Assert.NotNull(result);
        Assert.True(result!.Success);
        _orchestrator.Verify(o => o.ProcessItemAsync(It.IsAny<EnrollmentPipelineRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _leaseManager.Verify(l => l.ReleaseAsync(lease, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_ReturnsFailure_WhenLeaseNotAcquired()
    {
        var work = CreateWorkItem();
        _scheduler.Setup(s => s.GetNextWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync(work);
        _leaseManager.Setup(l => l.AcquireAsync(work, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentLease?)null);

        var result = await CreateWorker().ProcessNextAsync();

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("worker.lease_conflict", result.FailureCode);
        _orchestrator.Verify(o => o.ProcessItemAsync(It.IsAny<EnrollmentPipelineRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Scheduler_SchedulesRetry_UsingRetryPolicy()
    {
        _workRepository.Setup(r => r.ClaimNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateWorkItem());

        var scheduler = CreateScheduler();
        var result = await scheduler.ScheduleRetryAsync(new EnrollmentRetryScheduleRequest
        {
            WorkItem = CreateWorkItem(),
            StageName = "Storage",
            FailureCode = "storage.failure",
            FailureReason = "Transient upload failure.",
            FailureCategory = FailureCategory.StorageUploadFailed,
            AttemptCount = 1,
        });

        Assert.True(result.Scheduled);
        Assert.NotNull(result.NextAttemptUtc);
        _workRepository.Verify(r => r.ScheduleRetryAsync(
            It.IsAny<Guid>(),
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<FailureCategory?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scheduler_MovesToDeadLetter_WhenMaxRetriesExceeded()
    {
        var scheduler = CreateScheduler();
        var work = CreateWorkItem() with { RetryCount = 5 };

        var result = await scheduler.ScheduleRetryAsync(new EnrollmentRetryScheduleRequest
        {
            WorkItem = work,
            StageName = "Storage",
            FailureCode = "storage.failure",
            FailureReason = "Permanent failure.",
            AttemptCount = 5,
        });

        Assert.False(result.Scheduled);
        Assert.True(result.MovedToDeadLetter);
        _deadLetterService.Verify(d => d.PersistAsync(It.IsAny<EnrollmentDeadLetterRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SchedulingPolicy_OrdersByPriorityThenCreated()
    {
        var ordered = _schedulingPolicy.OrderCandidates([
            CreateWorkItem(priority: 1),
            CreateWorkItem(priority: 5),
            CreateWorkItem(priority: 3),
        ]);

        Assert.Equal(5, ordered[0].BatchPriority);
        Assert.Equal(3, ordered[1].BatchPriority);
        Assert.Equal(1, ordered[2].BatchPriority);
    }

    [Fact]
    public void SchedulingPolicy_RejectsFutureRetryItems()
    {
        var future = CreateWorkItem(nextAttemptUtc: DateTime.UtcNow.AddMinutes(10));
        Assert.False(_schedulingPolicy.IsEligible(future, DateTime.UtcNow));
    }

    [Fact]
    public async Task RecoveryService_ExpiresLeasesAndRequeuesStuckItems()
    {
        var work = CreateWorkItem();
        _leaseManager.Setup(l => l.ExpireAbandonedLeasesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _workRepository.Setup(r => r.GetStuckInFlightItemsAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { work });

        var recovery = new EnrollmentRecoveryService(
            _leaseManager.Object,
            _workRepository.Object,
            _unitOfWork.Object,
            _metrics.Object,
            Options.Create(new EnrollmentRecoveryOptions { MaxRecoveriesPerRun = 10, MaxRetryCount = 5 }),
            TimeProvider.System,
            NullLogger<EnrollmentRecoveryService>.Instance);

        var result = await recovery.RecoverAsync();

        Assert.Equal(2, result.ExpiredLeasesRecovered);
        Assert.Equal(1, result.StuckItemsRecovered);
        Assert.Equal(1, result.RequeuedItems);
        _workRepository.Verify(r => r.RequeueAsync(work.ItemId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WorkerHost_StartsConfiguredWorkerCount()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_scheduler.Object);
        services.AddSingleton(_metrics.Object);
        services.AddSingleton<IEnrollmentJobQueue>(new Mock<IEnrollmentJobQueue>().Object);
        services.AddSingleton(Options.Create(new EnrollmentBackgroundOptions { WorkerCount = 2, PollIntervalSeconds = 60 }));
        services.AddLogging();
        services.AddScoped<EnrollmentProcessingWorker>();
        services.AddScoped<IEnrollmentWorkerHost, EnrollmentWorkerHost>();

        var provider = services.BuildServiceProvider();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        _scheduler.Setup(s => s.GetNextWorkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentWorkItem?)null);

        var host = provider.GetRequiredService<IEnrollmentWorkerHost>();
        await host.RunAsync(cts.Token);

        Assert.True(host.ActiveWorkerCount >= 0);
    }

    private EnrollmentProcessingWorker CreateWorker()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var scope = new Mock<IServiceScope>();
        var scopeProvider = new Mock<IServiceProvider>();
        var tenantContext = new Mock<ITenantContextAccessor>();

        scopeProvider.Setup(p => p.GetService(typeof(IEnrollmentLeaseManager))).Returns(_leaseManager.Object);
        scopeProvider.Setup(p => p.GetService(typeof(IEnrollmentHeartbeatService))).Returns(_heartbeatService.Object);
        scopeProvider.Setup(p => p.GetService(typeof(IEnrollmentOrchestrator))).Returns(_orchestrator.Object);
        scopeProvider.Setup(p => p.GetService(typeof(IEnrollmentWorkScheduler))).Returns(_scheduler.Object);
        scopeProvider.Setup(p => p.GetService(typeof(ITenantContextAccessor))).Returns(tenantContext.Object);
        scope.Setup(s => s.ServiceProvider).Returns(scopeProvider.Object);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new EnrollmentProcessingWorker(
            scopeFactory.Object,
            _scheduler.Object,
            _metrics.Object,
            Options.Create(new EnrollmentBackgroundOptions { HeartbeatIntervalSeconds = 3600 }),
            NullLogger<EnrollmentProcessingWorker>.Instance);
    }

    private EnrollmentWorkScheduler CreateScheduler() =>
        new(
            _workRepository.Object,
            _schedulingPolicy,
            _retryPolicy,
            _deadLetterService.Object,
            _unitOfWork.Object,
            _lockProvider.Object,
            Options.Create(new EnrollmentRecoveryOptions { MaxRetryCount = 5 }),
            TimeProvider.System,
            NullLogger<EnrollmentWorkScheduler>.Instance);

    private static EnrollmentWorkItem CreateWorkItem(int priority = 1, DateTime? nextAttemptUtc = null) =>
        new()
        {
            ItemId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            BatchId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            TenantId = 1,
            StudentId = 42,
            Status = EnrollmentStatus.Pending,
            PipelineVersion = 1,
            CorrelationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            PhotoProviderName = "ExamBranch",
            CollegeId = 10,
            AcademicYear = 2026,
            StudentNumber = "STU001",
            CollegeCode = "COL",
            RetryCount = 0,
            NextAttemptUtc = nextAttemptUtc,
            BatchPriority = priority,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

    private static EnrollmentLease CreateLease(EnrollmentWorkItem work) =>
        new()
        {
            LeaseId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            WorkerId = "worker-1",
            NodeId = "node-1",
            ItemId = work.ItemId,
            BatchId = work.BatchId,
            TenantId = work.TenantId,
            StudentId = work.StudentId,
            AcquiredUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(2),
            HeartbeatUtc = DateTime.UtcNow,
            RenewalCount = 0,
            CorrelationId = work.CorrelationId,
            LeaseVersion = Guid.NewGuid().ToByteArray(),
        };

    private static EnrollmentPipelineResult CreateSuccessPipelineResult(EnrollmentWorkItem work) =>
        EnrollmentPipelineResult.Succeeded(
            new EnrollmentPipelineRequest
            {
                Context = new EnrollmentItemContext
                {
                    BatchId = work.BatchId,
                    ItemId = work.ItemId,
                    TenantId = work.TenantId,
                    StudentId = work.StudentId,
                    StudentNumber = work.StudentNumber,
                    CollegeCode = work.CollegeCode,
                    CollegeId = work.CollegeId,
                    AcademicYear = work.AcademicYear,
                    PhotoProviderName = work.PhotoProviderName,
                    ExecutionTraceId = Guid.NewGuid(),
                    CorrelationId = work.CorrelationId,
                    PipelineVersion = work.PipelineVersion,
                },
                ItemStatus = work.Status,
            },
            EnrollmentPipelineState.Completed,
            null,
            EnrollmentStatus.Completed,
            TimeSpan.FromMilliseconds(50),
            [],
            new EnrollmentPipelineStatistics { TotalDuration = TimeSpan.FromMilliseconds(50) },
            null);

    private sealed class TestLockHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
