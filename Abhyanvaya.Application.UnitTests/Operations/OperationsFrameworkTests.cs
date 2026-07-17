using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Operations;
using Abhyanvaya.Infrastructure.Operations.HealthProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Operations;

public sealed class OperationsFrameworkTests
{
    [Fact]
    public void AITraceContext_IsImmutable()
    {
        var context = new AITraceContext
        {
            TraceId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CurrentSpanId = Guid.NewGuid(),
        };

        var child = context with { WorkerId = "worker-1" };

        Assert.Null(context.WorkerId);
        Assert.Equal("worker-1", child.WorkerId);
    }

    [Fact]
    public void AIHealthRegistry_Register_IsIdempotent()
    {
        var registry = new AIHealthRegistry();
        var provider = new Mock<IAIHealthCheckProvider>();
        provider.Setup(p => p.ComponentName).Returns("Test");

        registry.Register(provider.Object);
        registry.Register(provider.Object);

        Assert.Single(registry.RegisteredComponents);
    }

    [Fact]
    public async Task AIHealthService_ResolvesOverallOffline_WhenAnyProviderOffline()
    {
        var registry = new AIHealthRegistry();
        registry.Register(CreateProvider("A", AIHealthStatus.Live));
        registry.Register(CreateProvider("B", AIHealthStatus.Offline));

        var service = new AIHealthService(registry, NullLogger<AIHealthService>.Instance);
        var report = await service.GetPlatformHealthAsync();

        Assert.Equal(AIHealthStatus.Offline, report.OverallStatus);
        Assert.Equal(2, report.Checks.Count);
    }

    [Fact]
    public void AITracingService_StartSpan_TracksSpans()
    {
        var tracing = new AITracingService(NullLogger<AITracingService>.Instance);
        var context = tracing.CreateContext();
        var childContext = tracing.StartSpan(context, "recognition.execute", "Recognition");

        var spans = tracing.GetActiveSpans(context.TraceId);
        Assert.Single(spans);
        Assert.Equal(childContext.CurrentSpanId, spans[0].SpanId);
    }

    [Fact]
    public async Task AIAlertManager_RaisesAndListsAlerts()
    {
        var manager = new AIAlertManager(new DefaultAlertPolicy(), NullLogger<AIAlertManager>.Instance);
        await manager.RaiseAlertAsync(new OperationalAlert
        {
            AlertId = "alert-1",
            Severity = AlertSeverity.Critical,
            Component = "Database",
            Message = "offline",
            CreatedUtc = DateTime.UtcNow,
        });

        var alerts = await manager.GetActiveAlertsAsync();
        Assert.Single(alerts);
        Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);
    }

    [Fact]
    public void DefaultFeatureFlagPolicy_RespectsTenantRollout()
    {
        var policy = new DefaultFeatureFlagPolicy();
        var flag = new FeatureFlagState
        {
            FlagKey = "experimental.models",
            Enabled = true,
            RolloutPercentage = 10,
        };

        Assert.True(policy.Evaluate(flag, 5, "production"));
        Assert.False(policy.Evaluate(flag, 95, "production"));
    }

    [Fact]
    public async Task OperationalRunbookService_ReturnsKnownScenario()
    {
        var service = new OperationalRunbookService();
        var runbook = await service.GetRunbookAsync("worker.failure");

        Assert.Equal("Worker Failure", runbook.Scenario);
        Assert.NotEmpty(runbook.RecoverySteps);
    }

    [Fact]
    public async Task AICapacityPlanner_GeneratesArchitectureOnlyForecast()
    {
        var metrics = new Mock<IAIMetricsCollector>();
        metrics.Setup(m => m.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalMetricsSnapshot
            {
                RecognitionRequests = 100,
                AttendanceSessions = 20,
            });

        var planner = new AICapacityPlanner(metrics.Object, NullLogger<AICapacityPlanner>.Instance);
        var report = await planner.GenerateReportAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        Assert.Equal(100, report.PeakRecognition);
        Assert.Contains("Architecture-only", report.ForecastNote!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AIProductionVerificationService_ReturnsPassFailReport()
    {
        var health = new Mock<IAIHealthService>();
        health.Setup(h => h.GetPlatformHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIPlatformHealthReport
            {
                OverallStatus = AIHealthStatus.Ready,
                Checks = new[]
                {
                    new AIHealthCheckResult
                    {
                        ComponentName = AIOperationsComponents.Database,
                        Status = AIHealthStatus.Ready,
                        Version = "1",
                        Duration = TimeSpan.Zero,
                    },
                },
                GeneratedUtc = DateTime.UtcNow,
            });

        var flags = new Mock<IAIFeatureFlagProvider>();
        flags.Setup(f => f.IsEnabledAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var diagnostics = new Mock<IAIDiagnosticsService>();
        diagnostics.Setup(d => d.GenerateDiagnosticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticsReport
            {
                DependencyGraph = new[] { "Platform -> Database" },
            });

        var verifier = new AIProductionVerificationService(
            health.Object,
            flags.Object,
            diagnostics.Object);

        var report = await verifier.VerifyAsync();

        Assert.NotNull(report.OverallStatus);
        Assert.NotEmpty(report.Checks);
    }

    [Fact]
    public void AIMetricsCollector_Increment_IsThreadSafe()
    {
        var context = new Mock<IApplicationDbContext>();
        var collector = new AIMetricsCollector(context.Object, NullLogger<AIMetricsCollector>.Instance);

        Parallel.For(0, 100, _ => collector.Increment("failures"));
    }

    private static IAIHealthCheckProvider CreateProvider(string name, AIHealthStatus status)
    {
        var mock = new Mock<IAIHealthCheckProvider>();
        mock.Setup(p => p.ComponentName).Returns(name);
        mock.Setup(p => p.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIHealthCheckResult
            {
                ComponentName = name,
                Status = status,
                Version = "test",
                Duration = TimeSpan.Zero,
            });
        return mock.Object;
    }
}
