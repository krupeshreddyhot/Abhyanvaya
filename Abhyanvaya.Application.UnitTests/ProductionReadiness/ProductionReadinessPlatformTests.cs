using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.ProductionReadiness;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.ProductionReadiness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Abhyanvaya.Application.UnitTests.ProductionReadiness;

public sealed class ProductionReadinessPlatformTests
{
    [Fact]
    public void ProductionReadinessPolicy_UsesConfiguredValues()
    {
        var policy = new ConfigurableProductionReadinessPolicy(Options.Create(new ProductionReadinessPolicyOptions
        {
            MinimumHealthScore = 0.9m,
            MaximumQueueDepth = 500,
            MaximumLatencyMilliseconds = 2500,
        }));

        Assert.Equal(0.9m, policy.MinimumHealthScore);
        Assert.Equal(500, policy.MaximumQueueDepth);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), policy.MaximumLatency);
    }

    [Fact]
    public async Task SecurityValidationService_ValidatesJwtAndTenantIsolation()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-key-minimum-length",
                ["Jwt:Issuer"] = "issuer",
                ["Jwt:Audience"] = "audience",
                ["ArtifactStorage:R2:SecretAccessKey"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IJwtService>(Mock.Of<IJwtService>());
        services.AddSingleton<ITenantContextAccessor>(Mock.Of<ITenantContextAccessor>());
        services.AddSingleton<ICurrentUserService>(Mock.Of<ICurrentUserService>());
        var provider = services.BuildServiceProvider();

        var service = new SecurityValidationService(configuration, provider);
        var report = await service.ValidateAsync(CreateContext());

        Assert.Equal(ProductionCheckStatus.Pass, report.OverallStatus);
        Assert.Contains(report.Checks, c => c.Name == "TenantIsolation" && c.Status == ProductionCheckStatus.Pass);
    }

    [Fact]
    public async Task GoLiveCertificationService_ApprovesWhenChecksPass()
    {
        var context = CreateContext();
        var certification = await BuildCertificationService(allPass: true).CertifyAsync(context);

        Assert.Equal("GO LIVE APPROVED", certification.Decision);
        Assert.True(certification.OverallScore >= 0.8m);
        Assert.Equal(ProductionReadinessState.ProductionCertified, certification.State);
    }

    [Fact]
    public async Task GoLiveCertificationService_BlocksWhenChecksFail()
    {
        var context = CreateContext();
        var certification = await BuildCertificationService(allPass: false).CertifyAsync(context);

        Assert.Equal("GO LIVE BLOCKED", certification.Decision);
        Assert.Equal(ProductionReadinessState.Failed, certification.State);
        Assert.NotEmpty(certification.CriticalIssues!);
    }

    [Fact]
    public async Task ProductionValidationScenarioRunner_ValidatesRecoveryScenarios()
    {
        var runbookService = new Mock<IOperationalRunbookService>();
        runbookService.Setup(r => r.GetRunbookAsync("database.failure", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalRunbook
            {
                Scenario = "Database Failure",
                RecoverySteps = ["Step 1", "Step 2"],
            });
        runbookService.Setup(r => r.GetRunbookAsync(It.IsNotIn("database.failure"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        var resilience = new Mock<IAIResilienceManager>();
        resilience.Setup(r => r.GetPolicyType("default")).Returns(ResiliencePolicyType.Retry);

        var runner = new ProductionValidationScenarioRunner(
            resilience.Object,
            runbookService.Object,
            new ConfigurableProductionReadinessPolicy(Options.Create(new ProductionReadinessPolicyOptions())));

        var results = await runner.RunScenariosAsync(CreateContext());
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Scenario == "database.failure" && r.RecoveryVerified);
    }

    [Fact]
    public async Task LoadTestingCoordinator_ExecutesConfiguredLoad()
    {
        var metrics = new Mock<IAIMetricsCollector>();
        metrics.Setup(m => m.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalMetricsSnapshot
            {
                AverageLatency = TimeSpan.FromMilliseconds(100),
                ThroughputPerMinute = 600,
                QueueDepth = 10,
                CpuPercent = 40m,
            });

        var coordinator = new LoadTestingCoordinator(
            metrics.Object,
            new Mock<IArtifactUploadQueue>().Object,
            new ConfigurableProductionReadinessPolicy(Options.Create(new ProductionReadinessPolicyOptions
            {
                LoadTestEnrollmentCount = 100,
                LoadTestConcurrentUploads = 50,
            })),
            NullLogger<LoadTestingCoordinator>.Instance);

        var report = await coordinator.ExecuteAsync(CreateContext());
        Assert.Equal(ProductionCheckStatus.Pass, report.OverallStatus);
        Assert.True(report.Statistics.RequestsPerSecond > 0);
    }

    private static GoLiveCertificationService BuildCertificationService(bool allPass)
    {
        var status = allPass ? ProductionCheckStatus.Pass : ProductionCheckStatus.Fail;
        var check = new ValidationCheckResult { Name = "Test", Status = status, Detail = "detail" };

        var deployment = new Mock<IDeploymentVerificationService>();
        deployment.Setup(s => s.VerifyAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeploymentVerificationReport
            {
                Context = CreateContext(),
                Checks = [check],
                OverallStatus = status,
                GeneratedUtc = DateTime.UtcNow,
            });

        var smoke = new Mock<IProductionSmokeTestService>();
        smoke.Setup(s => s.RunAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmokeTestReport
            {
                Context = CreateContext(),
                Checks = [check with { Name = "Database" }],
                OverallStatus = status,
                GeneratedUtc = DateTime.UtcNow,
            });

        var performance = new Mock<IPerformanceValidationService>();
        performance.Setup(s => s.ValidateAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PerformanceValidationReport
            {
                Context = CreateContext(),
                Statistics = new ProductionStatistics { AverageLatency = TimeSpan.FromMilliseconds(100), QueueDepth = 1 },
                Checks = [check],
                OverallStatus = status,
                GeneratedUtc = DateTime.UtcNow,
            });

        var security = new Mock<ISecurityValidationService>();
        security.Setup(s => s.ValidateAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecurityValidationReport
            {
                Context = CreateContext(),
                Checks = [check],
                OverallStatus = status,
                GeneratedUtc = DateTime.UtcNow,
            });

        var backup = new Mock<IBackupVerificationService>();
        backup.Setup(s => s.VerifyAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupVerificationReport
            {
                Context = CreateContext(),
                Checks = [check],
                OverallStatus = status,
                GeneratedUtc = DateTime.UtcNow,
            });

        var recovery = new Mock<IDisasterRecoveryValidator>();
        recovery.Setup(s => s.ValidateAsync(It.IsAny<DeploymentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DisasterRecoveryReport
            {
                Context = CreateContext(),
                Checks = [check],
                OverallStatus = status,
                GeneratedUtc = DateTime.UtcNow,
            });

        var capacity = new Mock<ICapacityPlanningService>();
        capacity.Setup(s => s.GenerateForecastAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CapacityForecast
            {
                CpuForecastPercent = 50m,
                MemoryForecastPercent = 60m,
                StorageForecastBytes = 1024,
                WorkerForecast = 4,
                QueueForecast = 10,
                GrowthForecastPercent = 12m,
            });

        return new GoLiveCertificationService(
            deployment.Object,
            smoke.Object,
            performance.Object,
            security.Object,
            backup.Object,
            recovery.Object,
            new ConfigurableProductionReadinessPolicy(Options.Create(new ProductionReadinessPolicyOptions())),
            capacity.Object);
    }

    private static DeploymentContext CreateContext() =>
        new()
        {
            DeploymentId = Guid.NewGuid(),
            Environment = "Production",
            Version = "1.0.0",
            BuildNumber = "100",
            GitCommit = "abc123",
            DeploymentTime = DateTime.UtcNow,
            Operator = "ops",
            CorrelationId = Guid.NewGuid(),
            TraceId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
        };
}
