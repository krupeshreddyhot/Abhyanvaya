using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.ProductionReadiness;
using Abhyanvaya.Infrastructure.ArtifactStorage;
using Abhyanvaya.Infrastructure.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ProductionReadiness;

public sealed class EnrollmentConfigurationValidator : IEnrollmentConfigurationValidator
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ArtifactStorageOptions _artifactStorageOptions;
    private readonly ILogger<EnrollmentConfigurationValidator> _logger;

    public EnrollmentConfigurationValidator(
        IConfiguration configuration,
        IHostEnvironment environment,
        IOptions<ArtifactStorageOptions> artifactStorageOptions,
        ILogger<EnrollmentConfigurationValidator> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _artifactStorageOptions = artifactStorageOptions.Value;
        _logger = logger;
    }

    public Task<StartupValidationReport> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<StartupValidationCheck>
        {
            CheckKey("Jwt:Key", "JwtSecret"),
            CheckKey("Jwt:Issuer", "JwtIssuer"),
            CheckKey("Jwt:Audience", "JwtAudience"),
            CheckExamBranch(),
            CheckStorage(),
            CheckRedis(),
            CheckWorkers(),
        };

        var overall = checks.Any(c => c.Severity == StartupValidationSeverity.Fail)
            ? StartupValidationSeverity.Fail
            : checks.Any(c => c.Severity == StartupValidationSeverity.Warning)
                ? StartupValidationSeverity.Warning
                : StartupValidationSeverity.Pass;

        if (overall == StartupValidationSeverity.Fail)
        {
            _logger.LogError("Enrollment startup validation failed with {Count} failing checks.", checks.Count(c => c.Severity == StartupValidationSeverity.Fail));
        }

        return Task.FromResult(new StartupValidationReport
        {
            Checks = checks,
            OverallSeverity = overall,
            GeneratedUtc = DateTime.UtcNow,
        });
    }

    private StartupValidationCheck CheckExamBranch()
    {
        var template = _configuration["StudentPhotoProvider:ExamBranch:BaseUrlTemplate"];
        if (string.IsNullOrWhiteSpace(template))
        {
            return Severity("ExamBranch", "ExamBranch photo URL template is not configured.", StartupValidationSeverity.Fail);
        }

        if (!template.Contains("{collegeCode}", StringComparison.OrdinalIgnoreCase)
            || !template.Contains("{academicYear}", StringComparison.OrdinalIgnoreCase)
            || !template.Contains("{studentNumber}", StringComparison.OrdinalIgnoreCase))
        {
            return Severity("ExamBranchTemplate", "ExamBranch template must include {collegeCode}, {academicYear}, and {studentNumber}.", StartupValidationSeverity.Fail);
        }

        return Pass("ExamBranch", "ExamBranch photo provider configured.");
    }

    private StartupValidationCheck CheckStorage()
    {
        var provider = ArtifactStorageProviderSelection.ResolveProviderName(_artifactStorageOptions, _environment);
        if (provider == LocalArtifactStorageProvider.ProviderId)
        {
            return _environment.IsProduction()
                ? Severity("ArtifactStorage", "Production must not use local artifact storage.", StartupValidationSeverity.Fail)
                : Pass("ArtifactStorage", "Local artifact storage enabled for development.");
        }

        var endpoint = _configuration["ArtifactStorage:R2:Endpoint"];
        var accessKey = _configuration["ArtifactStorage:R2:AccessKeyId"];
        var secret = _configuration["ArtifactStorage:R2:SecretAccessKey"];
        var bucket = _configuration["ArtifactStorage:Bucket"] ?? _configuration["ArtifactStorage:R2:Bucket"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(bucket))
        {
            return Severity("CloudflareR2", "Cloudflare R2 storage is not fully configured.", StartupValidationSeverity.Fail);
        }

        return Pass("CloudflareR2", "Cloudflare R2 storage configured.");
    }

    private StartupValidationCheck CheckRedis()
    {
        var useRedis = _configuration.GetValue<bool>("UseRedis");
        if (_environment.IsProduction() && !useRedis)
        {
            return Severity("Redis", "UseRedis must be enabled in production for tenant context persistence.", StartupValidationSeverity.Fail);
        }

        if (!useRedis)
        {
            return Severity("Redis", "Redis disabled — tenant context uses in-memory store.", StartupValidationSeverity.Warning);
        }

        return Pass("Redis", "Redis enabled.");
    }

    private StartupValidationCheck CheckWorkers()
    {
        var enabled = _configuration.GetValue<bool>("EnrollmentBackground:Enabled", true);
        if (_environment.IsProduction() && !enabled)
        {
            return Severity("EnrollmentWorkers", "Enrollment background workers are disabled.", StartupValidationSeverity.Fail);
        }

        if (!enabled)
        {
            return Severity("EnrollmentWorkers", "Enrollment background workers disabled.", StartupValidationSeverity.Warning);
        }

        return Pass("EnrollmentWorkers", "Enrollment background workers enabled.");
    }

    private StartupValidationCheck CheckKey(string key, string name)
    {
        var value = _configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? Severity(name, $"Missing configuration value for {key}.", StartupValidationSeverity.Fail)
            : Pass(name, "Configured.");
    }

    private StartupValidationCheck CheckUrl(string key, string name)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return Severity(name, $"Missing configuration value for {key}.", StartupValidationSeverity.Fail);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out _)
            ? Pass(name, "Valid absolute URL template.")
            : Severity(name, "URL template is not a valid absolute URI.", StartupValidationSeverity.Fail);
    }

    private static StartupValidationCheck Pass(string name, string detail) =>
        new() { Name = name, Severity = StartupValidationSeverity.Pass, Detail = detail };

    private static StartupValidationCheck Severity(string name, string detail, StartupValidationSeverity severity) =>
        new() { Name = name, Severity = severity, Detail = detail };
}

public sealed class EnrollmentStartupValidationHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<EnrollmentStartupValidationHostedService> _logger;

    public EnrollmentStartupValidationHostedService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IHostApplicationLifetime lifetime,
        ILogger<EnrollmentStartupValidationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var validator = scope.ServiceProvider.GetRequiredService<IEnrollmentConfigurationValidator>();
        var report = await validator.ValidateAsync(cancellationToken);
        foreach (var check in report.Checks.Where(c => c.Severity != StartupValidationSeverity.Pass))
        {
            if (check.Severity == StartupValidationSeverity.Fail)
            {
                _logger.LogError("Startup validation failed: {Name} — {Detail}", check.Name, check.Detail);
            }
            else
            {
                _logger.LogWarning("Startup validation warning: {Name} — {Detail}", check.Name, check.Detail);
            }
        }

        if (_environment.IsProduction() && report.OverallSeverity == StartupValidationSeverity.Fail)
        {
            _logger.LogCritical("Aborting startup due to failed production configuration validation.");
            _lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
