using Abhyanvaya.Application.AIOperations;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<StorageHealthCheckProvider> _logger;

    public StorageHealthCheckProvider(ILogger<StorageHealthCheckProvider> logger)
    {
        _logger = logger;
    }

    public override string ComponentName => AIOperationsComponents.Storage;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Storage health check executed");
        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Ready,
            Version = "local",
            Duration = DateTime.UtcNow - startedUtc,
        });
    }
}

public sealed class RecognitionHealthCheckProvider : BaseHealthCheckProvider
{
    private readonly IApplicationDbContext _context;

    public RecognitionHealthCheckProvider(IApplicationDbContext context)
    {
        _context = context;
    }

    public override string ComponentName => AIOperationsComponents.Recognition;

    protected override async Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
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
    public override string ComponentName => AIOperationsComponents.Workers;

    protected override Task<AIHealthCheckResult> CheckCoreAsync(DateTime startedUtc, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AIHealthCheckResult
        {
            ComponentName = ComponentName,
            Status = AIHealthStatus.Live,
            Version = "2.2",
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
