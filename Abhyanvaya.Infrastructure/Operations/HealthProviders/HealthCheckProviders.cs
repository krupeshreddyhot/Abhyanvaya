using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.ArtifactStorage;
using Abhyanvaya.Infrastructure.Enrollment.Background;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Operations.HealthProviders;

public abstract class BaseHealthCheckProvider : IAIHealthCheckProvider
{
    public abstract string ComponentName { get; }

    public async Task<AIHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        try
        {
            return await CheckCoreAsync(started, cancellationToken);
        }
        catch (Exception ex)
        {
            return new AIHealthCheckResult
            {
                ComponentName = ComponentName,
                Status = AIHealthStatus.Offline,
                Version = "unknown",
                Message = ex.Message,
                Duration = DateTime.UtcNow - started,
            };
        }
    }

    protected abstract Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken);
}

public sealed class DatabaseHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DatabaseHealthCheckProvider> _logger;

    public DatabaseHealthCheckProvider(IApplicationDbContext context, ILogger<DatabaseHealthCheckProvider> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override string ComponentName => AIOperationsComponents.Database;

    protected override async Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        var canQuery = await _context.Users.AsNoTracking().AnyAsync(cancellationToken);
        _logger.LogInformation("Database health check canQuery={CanQuery}", canQuery);
        return new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = canQuery ? AIHealthStatus.Ready : AIHealthStatus.Offline,
            Version = "ef-core",
            Duration = DateTime.UtcNow - startedUtc,
        };
    }
}

public sealed class StorageHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ArtifactStorageOptions _storageOptions;
    private readonly ILogger<StorageHealthCheckProvider> _logger;

    public StorageHealthCheckProvider(
        IConfiguration configuration,
        IHostEnvironment environment,
        IOptions<ArtifactStorageOptions> storageOptions,
        ILogger<StorageHealthCheckProvider> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public override string ComponentName => AIOperationsComponents.Storage;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        var provider = ArtifactStorageProviderSelection.ResolveProviderName(_storageOptions, _environment);
        if (provider == LocalArtifactStorageProvider.ProviderId)
        {
            return CheckLocalStorageAsync(startedUtc, cancellationToken);
        }

        return CheckR2StorageAsync(startedUtc);
    }

    private Task<AIHealthCheckResult> CheckLocalStorageAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var root = LocalArtifactStorageProvider.ResolveRootDirectory(_environment, _storageOptions);
            Directory.CreateDirectory(root);
            _logger.LogInformation("Local artifact storage health check passed root={Root}", root);
            return Task.FromResult(new AIHealthCheckResult
            {
                ComponentName = ComponentName,
                Status = AIHealthStatus.Ready,
                Version = "local",
                Message = $"Local artifact storage is ready at {root}.",
                Duration = DateTime.UtcNow - startedUtc,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local artifact storage health check failed");
            return Task.FromResult(new AIHealthCheckResult
            {
                ComponentName = ComponentName,
                Status = AIHealthStatus.Offline,
                Version = "local",
                Message = "Local artifact storage directory is not accessible.",
                Duration = DateTime.UtcNow - startedUtc,
            });
        }
    }

    private Task<AIHealthCheckResult> CheckR2StorageAsync(DateTime startedUtc)
    {
        var endpoint = _configuration["ArtifactStorage:R2:Endpoint"];
        var accessKeyId = _configuration["ArtifactStorage:R2:AccessKeyId"];
        var secretAccessKey = _configuration["ArtifactStorage:R2:SecretAccessKey"];

        var configured = !string.IsNullOrWhiteSpace(endpoint)
            && !string.IsNullOrWhiteSpace(accessKeyId)
            && !string.IsNullOrWhiteSpace(secretAccessKey);

        if (!configured)
        {
            _logger.LogWarning("Cloudflare R2 storage is not configured");
            return Task.FromResult(new AIHealthCheckResult
            {
                ComponentName = ComponentName,
                Status = AIHealthStatus.Offline,
                Version = "r2",
                Message = "Cloudflare R2 storage is not configured.",
                Duration = DateTime.UtcNow - startedUtc,
            });
        }

        _logger.LogInformation("Storage health check passed (R2 configured)");
        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Ready,
            Version = "r2",
            Duration = DateTime.UtcNow - startedUtc,
        });
    }
}

public sealed class RecognitionHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IApplicationDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly Microsoft.Extensions.Options.IOptions<InsightFace.InsightFaceOptions> _insightFaceOptions;

    public RecognitionHealthCheckProvider(
        IApplicationDbContext context,
        IHostEnvironment environment,
        Microsoft.Extensions.Options.IOptions<InsightFace.InsightFaceOptions> insightFaceOptions)
    {
        _context = context;
        _environment = environment;
        _insightFaceOptions = insightFaceOptions;
    }

    public override string ComponentName => AIOperationsComponents.Recognition;

    protected override async Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        if (!InsightFace.InsightFaceModelPathResolver.AllModelsPresent(_insightFaceOptions.Value, _environment))
        {
            return new AIHealthCheckResult
            {
                ComponentName = ComponentName,
                Status = AIHealthStatus.Offline,
                Version = _insightFaceOptions.Value.PipelineVersion,
                Message = "InsightFace ONNX models are missing from the configured model directory.",
                Duration = DateTime.UtcNow - startedUtc,
            };
        }

        var recentCount = await _context.AttendanceRecognitions
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Live,
            Version = "2.3",
            Dependencies = new Dictionary<string, string> { ["recentRecords"] = recentCount.ToString() },
            Duration = DateTime.UtcNow - startedUtc,
        };
    }
}

public sealed class EmbeddingEngineHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IHostEnvironment _environment;
    private readonly Microsoft.Extensions.Options.IOptions<InsightFace.InsightFaceOptions> _insightFaceOptions;

    public EmbeddingEngineHealthCheckProvider(
        IHostEnvironment environment,
        Microsoft.Extensions.Options.IOptions<InsightFace.InsightFaceOptions> insightFaceOptions)
    {
        _environment = environment;
        _insightFaceOptions = insightFaceOptions;
    }

    public override string ComponentName => AIOperationsComponents.EmbeddingEngine;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        var options = _insightFaceOptions.Value;
        var modelsPresent = InsightFace.InsightFaceModelPathResolver.AllModelsPresent(options, _environment);

        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = modelsPresent ? AIHealthStatus.Ready : AIHealthStatus.Offline,
            Version = options.PipelineVersion,
            Message = modelsPresent ? null : "InsightFace embedding model files are not deployed.",
            Duration = DateTime.UtcNow - startedUtc,
        });
    }
}

public sealed class AttendanceHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IApplicationDbContext _context;

    public AttendanceHealthCheckProvider(IApplicationDbContext context)
    {
        _context = context;
    }

    public override string ComponentName => AIOperationsComponents.Attendance;

    protected override async Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        var sessionCount = await _context.AttendanceSessions
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Live,
            Version = "2.4",
            Dependencies = new Dictionary<string, string> { ["sessions"] = sessionCount.ToString() },
            Duration = DateTime.UtcNow - startedUtc,
        };
    }
}

public sealed class EnrollmentHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IApplicationDbContext _context;

    public EnrollmentHealthCheckProvider(IApplicationDbContext context)
    {
        _context = context;
    }

    public override string ComponentName => AIOperationsComponents.Enrollment;

    protected override async Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        var batchCount = await _context.StudentEnrollmentBatches
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Ready,
            Version = "2.1",
            Dependencies = new Dictionary<string, string> { ["batches"] = batchCount.ToString() },
            Duration = DateTime.UtcNow - startedUtc,
        };
    }
}

public sealed class WorkerHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly EnrollmentBackgroundOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public WorkerHealthCheckProvider(
        IOptions<EnrollmentBackgroundOptions> options,
        IServiceProvider serviceProvider)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public override string ComponentName => AIOperationsComponents.Workers;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return Task.FromResult(new AIHealthCheckResult
            {
                ComponentName = ComponentName,
                Status = AIHealthStatus.Offline,
                Version = "2.2",
                Message = "Enrollment background workers are disabled (EnrollmentBackground:Enabled=false).",
                Duration = DateTime.UtcNow - startedUtc,
            });
        }

        var backgroundService = _serviceProvider
            .GetServices<IHostedService>()
            .OfType<EnrollmentBackgroundService>()
            .FirstOrDefault();

        var isRunning = backgroundService?.IsRunning == true;

        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = isRunning ? AIHealthStatus.Live : AIHealthStatus.Offline,
            Version = "2.2",
            Message = isRunning
                ? "Enrollment background workers are running."
                : "Enrollment background workers are not running.",
            Duration = DateTime.UtcNow - startedUtc,
        });
    }
}

public sealed class ModelRegistryHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IApplicationDbContext _context;

    public ModelRegistryHealthCheckProvider(IApplicationDbContext context)
    {
        _context = context;
    }

    public override string ComponentName => AIOperationsComponents.ModelRegistry;

    protected override async Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        var modelCount = await _context.AiModelDefinitions
            .AsNoTracking()
            .CountAsync(cancellationToken);

        return new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Ready,
            Version = "2.5",
            Dependencies = new Dictionary<string, string> { ["models"] = modelCount.ToString() },
            Duration = DateTime.UtcNow - startedUtc,
        };
    }
}

public sealed class VectorProviderHealthCheckProvider : BaseHealthCheckProvider
{
    public override string ComponentName => AIOperationsComponents.VectorProvider;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Ready,
            Version = "stub",
            Duration = DateTime.UtcNow - startedUtc,
        });
    }
}

public sealed class BackgroundJobsHealthCheckProvider : BaseHealthCheckProvider
{
    public override string ComponentName => AIOperationsComponents.BackgroundJobs;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Live,
            Version = "hosted-service",
            Duration = DateTime.UtcNow - startedUtc,
        });
    }
}

public sealed class GovernanceHealthCheckProvider : BaseHealthCheckProvider
{
    public override string ComponentName => AIOperationsComponents.Governance;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Ready,
            Version = "2.5",
            Duration = DateTime.UtcNow - startedUtc,
        });
    }
}
