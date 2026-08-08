using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public interface IAllocationHistoryService
{
    Task<IReadOnlyList<AllocationHistoryRow>> QueryAsync(AllocationHistoryFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>AI29.1C.5A — Centralized immutable version creation (alias of versioning service).</summary>
public interface IAllocationScenarioVersionService
{
    Task<AllocationScenarioVersionDto> AppendVersionAsync(
        Guid scenarioId,
        string reason,
        string? scenarioJson = null,
        string? configJson = null,
        string? traceJson = null,
        string? operation = null,
        bool persist = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllocationScenarioVersionDto>> ListAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

/// <summary>Backward-compatible alias used by AI29.1C.5 callers.</summary>
public interface IAllocationScenarioVersioningService : IAllocationScenarioVersionService;

public interface IAllocationReplayService
{
    Task<AllocationExecutionResult> ReplayAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

public interface IAllocationComparisonService
{
    Task<AllocationMultiCompareReport> CompareAsync(IReadOnlyList<Guid> scenarioIds, CancellationToken cancellationToken = default);
}

public interface IAllocationExplanationService
{
    Task<AllocationExplanationReport> ExplainAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

public interface IAllocationAnalyticsService
{
    Task<AllocationAnalyticsDto> GetAsync(string period = "AcademicYear", CancellationToken cancellationToken = default);
}

public interface IAllocationOpsDashboardService
{
    Task<AllocationOpsDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAllocationGovernanceService
{
    Task<AllocationGovernanceResult> EvaluateAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<AllocationGovernanceResult> ApproveWithGovernanceAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<AllocationGovernanceResult> RejectAsync(Guid scenarioId, string? reason = null, CancellationToken cancellationToken = default);
    Task<AllocationGovernanceResult> ReviewAsync(Guid scenarioId, string? notes = null, CancellationToken cancellationToken = default);
    Task<AllocationGovernanceResult> ArchiveAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<AllocationGovernanceResult> SaveAsync(Guid scenarioId, string? reason = null, CancellationToken cancellationToken = default);
    Task<AllocationGovernanceResult> MarkComparedAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

public interface IAllocationAuditService
{
    Task WriteAsync(
        string action,
        Guid? scenarioId,
        Guid? sessionId,
        int? version,
        string? contextVersion,
        string result,
        string? detail = null,
        bool persist = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AllocationAuditDto>> ListAsync(int take = 50, CancellationToken cancellationToken = default);
}

public sealed class AllocationAuditService : IAllocationAuditService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AllocationAuditService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task WriteAsync(
        string action,
        Guid? scenarioId,
        Guid? sessionId,
        int? version,
        string? contextVersion,
        string result,
        string? detail = null,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        await _db.AddAsync(new AllocationAuditEntry
        {
            AuditId = Guid.NewGuid(),
            Action = action,
            ScenarioId = scenarioId,
            SessionId = sessionId,
            VersionNumber = version,
            ContextVersion = contextVersion,
            Result = result,
            Detail = detail,
            OccurredAt = DateTime.UtcNow,
            ActorUserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        });
        if (persist)
            await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AllocationAuditDto>> ListAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        var rows = await _db.AllocationAuditEntries.AsNoTracking()
            .Where(a => a.TenantId == _currentUser.TenantId)
            .OrderByDescending(a => a.OccurredAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(a => new AllocationAuditDto
        {
            AuditId = a.AuditId,
            Action = a.Action,
            ScenarioId = a.ScenarioId,
            SessionId = a.SessionId,
            VersionNumber = a.VersionNumber,
            ContextVersion = a.ContextVersion,
            Result = a.Result,
            Detail = a.Detail,
            OccurredAt = a.OccurredAt,
            ActorUserId = a.ActorUserId,
        }).ToList();
    }
}

public sealed class AllocationScenarioVersioningService : IAllocationScenarioVersioningService, IAllocationScenarioVersionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AllocationScenarioVersioningService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AllocationScenarioVersionDto> AppendVersionAsync(
        Guid scenarioId,
        string reason,
        string? scenarioJson = null,
        string? configJson = null,
        string? traceJson = null,
        string? operation = null,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _db.AllocationEngineScenarios
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken)
            ?? throw new InvalidOperationException("Scenario not found.");

        var next = scenario.CurrentVersionNumber + 1;
        var json = scenarioJson ?? scenario.ScenarioJson;
        var cfg = configJson ?? "{}";
        var trace = traceJson ?? "[]";
        var op = operation ?? reason;
        var checksum = AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
        {
            ScenarioId = scenarioId,
            VersionNumber = next,
            ContextVersion = scenario.ContextVersion,
            ContextChecksum = scenario.ContextChecksum,
            StrategyConfigurationVersion = scenario.StrategyConfigurationVersion,
            ConstraintConfigurationVersion = scenario.ConstraintConfigurationVersion,
            LifecycleStatus = scenario.LifecycleStatus,
            Operation = op,
            Score = scenario.TotalScore,
            ScenarioJson = json,
            TraceJson = trace,
            ConfigJson = cfg,
        });
        var version = new AllocationScenarioVersion
        {
            ScenarioId = scenarioId,
            VersionNumber = next,
            ContextVersion = scenario.ContextVersion,
            ContextChecksum = scenario.ContextChecksum,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            Reason = reason,
            Operation = op,
            StrategyConfigurationVersion = scenario.StrategyConfigurationVersion,
            ConstraintConfigurationVersion = scenario.ConstraintConfigurationVersion,
            Score = scenario.TotalScore,
            Status = scenario.LifecycleStatus,
            Checksum = checksum,
            ScenarioJson = json,
            ConfigJson = cfg,
            TraceJson = trace,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        scenario.CurrentVersionNumber = next;
        scenario.ScenarioChecksum = checksum;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _db.AddAsync(version);
        if (persist)
            await _db.SaveChangesAsync(cancellationToken);
        return Map(version);
    }

    public async Task<IReadOnlyList<AllocationScenarioVersionDto>> ListAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.AllocationScenarioVersions.AsNoTracking()
            .Where(v => v.TenantId == _currentUser.TenantId && v.ScenarioId == scenarioId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    private static AllocationScenarioVersionDto Map(AllocationScenarioVersion v) => new()
    {
        ScenarioId = v.ScenarioId,
        VersionNumber = v.VersionNumber,
        ContextVersion = v.ContextVersion,
        ContextChecksum = v.ContextChecksum,
        CreatedAt = v.CreatedAt,
        CreatedBy = v.CreatedByUserId,
        Reason = v.Reason,
        Operation = v.Operation,
        StrategyConfigurationVersion = v.StrategyConfigurationVersion,
        ConstraintConfigurationVersion = v.ConstraintConfigurationVersion,
        Score = v.Score,
        Status = v.Status,
        Checksum = v.Checksum,
    };

    /// <summary>Legacy helper — prefer <see cref="AllocationCanonicalChecksum"/> for version integrity.</summary>
    public static string Sha256(string payload)
        => AllocationCanonicalChecksum.Sha256Utf8(payload);
}

public sealed class AllocationHistoryService : IAllocationHistoryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AllocationHistoryService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AllocationHistoryRow>> QueryAsync(
        AllocationHistoryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var q = _db.AllocationEngineScenarios.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId);

        if (filter.AcademicYearId is > 0) q = q.Where(s => s.AcademicYearId == filter.AcademicYearId);
        if (filter.CourseId is > 0) q = q.Where(s => s.CourseId == filter.CourseId);
        if (filter.GroupId is > 0) q = q.Where(s => s.GroupId == filter.GroupId);
        if (filter.SemesterId is > 0) q = q.Where(s => s.SemesterId == filter.SemesterId);
        if (filter.CreatedBy is > 0) q = q.Where(s => s.CreatedBy == filter.CreatedBy);
        if (filter.FromUtc is DateTime from) q = q.Where(s => s.GeneratedAt >= from);
        if (filter.ToUtc is DateTime to) q = q.Where(s => s.GeneratedAt <= to);
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(s => s.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.LifecycleStatus)) q = q.Where(s => s.LifecycleStatus == filter.LifecycleStatus);

        var rows = await q.OrderByDescending(s => s.GeneratedAt).Take(100).ToListAsync(cancellationToken);
        var sessions = await _db.AllocationEngineSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .Select(s => new { s.SessionId, s.GroupingMode })
            .ToDictionaryAsync(s => s.SessionId, s => s.GroupingMode, cancellationToken);

        return rows.Select(r => new AllocationHistoryRow
        {
            SessionId = r.SessionId,
            ScenarioId = r.ScenarioId,
            CreatedAt = r.GeneratedAt,
            Status = r.Status,
            LifecycleStatus = r.LifecycleStatus,
            Score = r.TotalScore,
            GroupingMode = sessions.GetValueOrDefault(r.SessionId) ?? "",
            VersionNumber = r.CurrentVersionNumber,
            ContextChecksum = r.ContextChecksum,
            AcademicYearId = r.AcademicYearId,
            CourseId = r.CourseId,
            GroupId = r.GroupId,
            SemesterId = r.SemesterId,
            CreatedBy = r.CreatedBy,
            Kind = "Scenario",
        }).ToList();
    }
}
