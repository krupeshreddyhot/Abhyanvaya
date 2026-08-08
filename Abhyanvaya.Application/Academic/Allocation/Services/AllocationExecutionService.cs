using System.Text.Json;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public interface IAllocationExecutionService
{
    Task<AllocationExecutionResult> RunAsync(
        AllocationScopeRequest scope,
        AllocationPipelineConfig? config = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllocationHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<AllocationExecutionResult?> GetSessionResultAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates context build + engine execution + persistence.
/// Engine itself never sees DbContext / operational services.
/// </summary>
public sealed class AllocationExecutionService : IAllocationExecutionService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ISectionAllocationContextBuilder _builder;
    private readonly IAllocationEngine _engine;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAllocationProgressPublisher _progress;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly IAllocationAuditService? _audit;

    public AllocationExecutionService(
        ISectionAllocationContextBuilder builder,
        IAllocationEngine engine,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAllocationProgressPublisher progress,
        IAcademicTelemetryService telemetry,
        IAllocationAuditService? audit = null)
    {
        _builder = builder;
        _engine = engine;
        _db = db;
        _currentUser = currentUser;
        _progress = progress;
        _telemetry = telemetry;
        _audit = audit;
    }

    public Task<AllocationExecutionResult> RunAsync(
        AllocationScopeRequest scope,
        AllocationPipelineConfig? config = null,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationEngineRun,
            "AllocationEngine.Run",
            ct => RunCoreAsync(scope, config ?? AllocationPipelineConfig.Default, ct),
            cancellationToken);

    private async Task<AllocationExecutionResult> RunCoreAsync(
        AllocationScopeRequest scope,
        AllocationPipelineConfig config,
        CancellationToken ct)
    {
        var context = await _builder.BuildAsync(scope, ct);
        var sessionId = Guid.NewGuid();
        var progress = new Progress<AllocationProgress>(p =>
        {
            _ = _progress.PublishProgressAsync(_currentUser.TenantId, p, ct);
        });

        var result = await _engine.ExecuteAsync(
            new AllocationExecutionContext
            {
                SessionId = sessionId,
                Context = context,
                Config = config,
                StartedAt = DateTime.UtcNow,
            },
            progress,
            ct);

        await PersistAsync(scope, config, result, ct);
        await _progress.PublishCompletedAsync(_currentUser.TenantId, result, ct);
        return result;
    }

    private async Task PersistAsync(
        AllocationScopeRequest scope,
        AllocationPipelineConfig config,
        AllocationExecutionResult result,
        CancellationToken ct)
    {
        var session = new AllocationEngineSession
        {
            SessionId = result.SessionId,
            ContextId = result.Scenario.ContextId,
            ContextChecksum = result.Scenario.ContextChecksum,
            Status = result.Status,
            GroupingMode = config.GroupingMode,
            ActiveScenarioId = result.ScenarioId,
            CompletedAt = DateTime.UtcNow,
            AcademicYearId = scope.AcademicYearId,
            CourseId = scope.CourseId,
            GroupId = scope.GroupId,
            SemesterId = scope.SemesterId,
            ConfigJson = JsonSerializer.Serialize(config, JsonOpts),
            TraceJson = JsonSerializer.Serialize(result.Trace, JsonOpts),
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        var scenarioJson = JsonSerializer.Serialize(result.Scenario, JsonOpts);
        var contextVersion = result.Scenario.ContextId.ToString("N")[..8];
        var scenarioChecksum = AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
        {
            ScenarioId = result.ScenarioId,
            VersionNumber = 1,
            ContextVersion = contextVersion,
            ContextChecksum = result.Scenario.ContextChecksum,
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            LifecycleStatus = AllocationScenarioLifecycle.Generated,
            Operation = AllocationAuditActions.CreateScenario,
            Score = result.Score.TotalScore,
            ScenarioJson = scenarioJson,
            TraceJson = session.TraceJson,
            ConfigJson = session.ConfigJson,
        });
        var scenario = new AllocationEngineScenario
        {
            ScenarioId = result.ScenarioId,
            SessionId = result.SessionId,
            ContextId = result.Scenario.ContextId,
            ContextChecksum = result.Scenario.ContextChecksum,
            // Status = execution/result; LifecycleStatus = governance (AI29.1C.5A).
            Status = result.Status,
            TotalScore = result.Score.TotalScore,
            ScenarioJson = scenarioJson,
            GeneratedAt = result.Scenario.GeneratedAt,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            CurrentVersionNumber = 1,
            LifecycleStatus = AllocationScenarioLifecycle.Generated,
            ContextVersion = contextVersion,
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            ScenarioChecksum = scenarioChecksum,
            AcademicYearId = scope.AcademicYearId,
            CourseId = scope.CourseId,
            GroupId = scope.GroupId,
            SemesterId = scope.SemesterId,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        var version = new AllocationScenarioVersion
        {
            ScenarioId = result.ScenarioId,
            VersionNumber = 1,
            ContextVersion = scenario.ContextVersion,
            ContextChecksum = scenario.ContextChecksum,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            Reason = "Initial scenario creation",
            Operation = AllocationAuditActions.CreateScenario,
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            Score = result.Score.TotalScore,
            Status = scenario.LifecycleStatus,
            Checksum = scenarioChecksum,
            ScenarioJson = scenarioJson,
            ConfigJson = session.ConfigJson,
            TraceJson = session.TraceJson,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(session);
        await _db.AddAsync(scenario);
        await _db.AddAsync(version);
        await _db.SaveChangesAsync(ct);

        if (_audit is not null)
        {
            await _audit.WriteAsync(
                AllocationAuditActions.Run,
                result.ScenarioId,
                result.SessionId,
                1,
                scenario.ContextVersion,
                result.Succeeded ? "Ok" : "CompletedWithErrors",
                persist: true,
                cancellationToken: ct);
            await _audit.WriteAsync(
                AllocationAuditActions.CreateScenario,
                result.ScenarioId,
                result.SessionId,
                1,
                scenario.ContextVersion,
                "Ok",
                "Initial scenario creation",
                persist: true,
                cancellationToken: ct);
        }
    }

    public async Task<IReadOnlyList<AllocationHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.AllocationEngineSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .OrderByDescending(s => s.CreatedDate)
            .Take(50)
            .ToListAsync(cancellationToken);
        var scores = await _db.AllocationEngineScenarios.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .Select(s => new { s.ScenarioId, s.TotalScore })
            .ToDictionaryAsync(s => s.ScenarioId, s => s.TotalScore, cancellationToken);

        return rows.Select(r => new AllocationHistoryItem
        {
            SessionId = r.SessionId,
            ScenarioId = r.ActiveScenarioId,
            CreatedAt = r.CreatedDate,
            Status = r.Status,
            Score = r.ActiveScenarioId is Guid id && scores.TryGetValue(id, out var sc) ? sc : 0,
            GroupingMode = r.GroupingMode,
        }).ToList();
    }

    public async Task<AllocationExecutionResult?> GetSessionResultAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _db.AllocationEngineSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SessionId == sessionId, cancellationToken);
        if (session is null) return null;
        var scenario = await _db.AllocationEngineScenarios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SessionId == sessionId, cancellationToken);
        if (scenario is null) return null;
        var parsed = JsonSerializer.Deserialize<AllocationScenario>(scenario.ScenarioJson, JsonOpts) ?? new AllocationScenario();
        var trace = JsonSerializer.Deserialize<AllocationTrace>(session.TraceJson, JsonOpts) ?? new AllocationTrace();
        return new AllocationExecutionResult
        {
            SessionId = sessionId,
            ScenarioId = scenario.ScenarioId,
            Succeeded = session.Status is "Completed" or "Accepted",
            Status = session.Status,
            Scenario = parsed,
            Trace = trace,
            Score = parsed.Score,
        };
    }
}
