using System.Text.Json;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public interface IAllocationSimulationService
{
    Task<AllocationExecutionResult> PreviewAsync(AllocationScopeRequest scope, AllocationPipelineConfig? config = null, CancellationToken cancellationToken = default);
    Task<AllocationComparisonReport> CompareAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<bool> RejectAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<bool> AcceptSimulationAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

public interface IAllocationApprovalService
{
    Task<AllocationDraft> ApproveAsync(Guid scenarioId, CancellationToken cancellationToken = default, bool persist = true);
}

public interface IAllocationSandboxService
{
    Task<AllocationSandboxItem> SaveAsync(Guid scenarioId, string name, string? tags = null, CancellationToken cancellationToken = default);
    Task<AllocationSandboxItem?> DuplicateAsync(Guid sandboxId, string? newName = null, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid sandboxId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AllocationSandboxItem>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<AllocationScenario?> ReplayAsync(Guid sandboxId, CancellationToken cancellationToken = default);
}

public interface IAllocationDashboardService
{
    Task<AllocationDashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface IAllocationReportService
{
    Task<byte[]> ExportAsync(string reportKind, string format, Guid? scenarioId = null, CancellationToken cancellationToken = default);
}

public sealed class AllocationSimulationService : IAllocationSimulationService
{
    private readonly IAllocationExecutionService _execution;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly IAllocationScenarioLifecycleService _lifecycle;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationSimulationService(
        IAllocationExecutionService execution,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicTelemetryService telemetry,
        IAllocationScenarioLifecycleService lifecycle)
    {
        _execution = execution;
        _db = db;
        _currentUser = currentUser;
        _telemetry = telemetry;
        _lifecycle = lifecycle;
    }

    public Task<AllocationExecutionResult> PreviewAsync(
        AllocationScopeRequest scope,
        AllocationPipelineConfig? config = null,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationSimulation,
            "Allocation.Simulate",
            ct => _execution.RunAsync(scope, config, ct),
            cancellationToken);

    public async Task<AllocationComparisonReport> CompareAsync(Guid scenarioId, CancellationToken cancellationToken = default)
        => await _telemetry.TrackAsync(
            AcademicOperations.AllocationComparison,
            "Allocation.Compare",
            async ct =>
            {
                var row = await _db.AllocationEngineScenarios.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, ct)
                    ?? throw new InvalidOperationException("Scenario not found.");
                var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts)
                    ?? throw new InvalidOperationException("Scenario JSON invalid.");

                // Rebuild original occupancy from FromSectionId distribution.
                var original = scenario.Recommendations
                    .GroupBy(r => r.FromSectionId ?? 0)
                    .Select(g =>
                    {
                        var max = scenario.SectionSummaries.FirstOrDefault(s => s.SectionId == g.Key)?.MaximumCapacity ?? 0;
                        var assigned = g.Count();
                        return new AllocationSectionSummary
                        {
                            SectionId = g.Key,
                            SectionCode = g.First().FromSectionCode ?? (g.Key == 0 ? "Unassigned" : g.Key.ToString()),
                            AssignedCount = assigned,
                            MaximumCapacity = max,
                            OccupancyPercent = max > 0 ? Math.Round(assigned * 100.0 / max, 2) : 0,
                        };
                    }).ToList();

                var origAvg = original.Count == 0 ? 0 : original.Average(x => x.OccupancyPercent);
                var allocAvg = scenario.SectionSummaries.Count == 0 ? 0 : scenario.SectionSummaries.Average(x => x.OccupancyPercent);
                var violations = scenario.Constraints.Where(c => !c.Satisfied).ToList();

                return new AllocationComparisonReport
                {
                    ScenarioId = scenarioId,
                    OriginalAverageOccupancy = Math.Round(origAvg, 2),
                    AllocatedAverageOccupancy = Math.Round(allocAvg, 2),
                    CapacityImprovement = Math.Round(allocAvg - origAvg, 2),
                    GenderBalanceScore = scenario.Score.GenderBalance,
                    PolicyComplianceScore = scenario.Score.PolicyCompliance,
                    OriginalSections = original,
                    AllocatedSections = scenario.SectionSummaries,
                    ConstraintViolations = violations,
                    Summary = $"Capacity Δ {allocAvg - origAvg:0.##}pp; violations={violations.Count}.",
                };
            },
            cancellationToken);

    public async Task<bool> RejectAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        // AI29.1C.5A — lifecycle transitions go through governance; keep engine reject as thin delegate.
        var row = await _db.AllocationEngineScenarios
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken);
        if (row is null) return false;
        if (!_lifecycle.CanTransition(row.LifecycleStatus, AllocationScenarioLifecycle.Rejected))
            return false;
        var result = await _lifecycle.TransitionTrackedAsync(
            row,
            AllocationScenarioLifecycle.Rejected,
            AllocationAuditActions.Reject,
            reason: "Rejected from simulation",
            createVersion: true,
            writeAudit: true,
            persist: true,
            cancellationToken: cancellationToken);
        return result.Success;
    }

    public async Task<bool> AcceptSimulationAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        // Simulation accept only — does NOT approve draft / live write.
        var row = await _db.AllocationEngineScenarios
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken);
        if (row is null) return false;
        var result = await _lifecycle.TransitionTrackedAsync(
            row,
            AllocationScenarioLifecycle.SimulationAccepted,
            AllocationAuditActions.Simulate,
            reason: "Simulation accepted",
            createVersion: true,
            writeAudit: true,
            persist: true,
            cancellationToken: cancellationToken);
        return result.Success;
    }
}

public sealed class AllocationApprovalService : IAllocationApprovalService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicTelemetryService _telemetry;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationApprovalService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _telemetry = telemetry;
    }

    public Task<AllocationDraft> ApproveAsync(Guid scenarioId, CancellationToken cancellationToken = default, bool persist = true)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationApproval,
            "Allocation.ApproveDraft",
            ct => ApproveCoreAsync(scenarioId, persist, ct),
            cancellationToken);

    private async Task<AllocationDraft> ApproveCoreAsync(Guid scenarioId, bool persist, CancellationToken ct)
    {
        var row = await _db.AllocationEngineScenarios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, ct)
            ?? throw new InvalidOperationException("Scenario not found.");
        var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts)
            ?? throw new InvalidOperationException("Invalid scenario.");

        var draft = new AllocationDraft
        {
            DraftId = Guid.NewGuid(),
            ScenarioId = scenarioId,
            SessionId = row.SessionId,
            CreatedAt = DateTime.UtcNow,
            ApprovedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            Status = "Draft",
            Recommendations = scenario.Recommendations,
        };

        await _db.AddAsync(new AllocationEngineDraft
        {
            DraftId = draft.DraftId,
            ScenarioId = draft.ScenarioId,
            SessionId = draft.SessionId,
            Status = "Draft",
            ApprovedBy = draft.ApprovedBy,
            DraftJson = JsonSerializer.Serialize(draft, JsonOpts),
            Note = draft.Note,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = draft.ApprovedBy,
        });
        if (persist)
            await _db.SaveChangesAsync(ct);
        return draft;
    }
}

public sealed class AllocationSandboxService : IAllocationSandboxService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAllocationScenarioLifecycleService _lifecycle;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationSandboxService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAllocationScenarioLifecycleService lifecycle)
    {
        _db = db;
        _currentUser = currentUser;
        _lifecycle = lifecycle;
    }

    public async Task<AllocationSandboxItem> SaveAsync(Guid scenarioId, string name, string? tags = null, CancellationToken cancellationToken = default)
    {
        var scenario = await _db.AllocationEngineScenarios
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken)
            ?? throw new InvalidOperationException("Scenario not found.");
        var item = new AllocationEngineSandboxItem
        {
            SandboxId = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? $"Scenario {scenarioId:N}"[..20] : name.Trim(),
            ScenarioId = scenarioId,
            SessionId = scenario.SessionId,
            Tags = tags,
            ScenarioJson = scenario.ScenarioJson,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(item);
        await _db.SaveChangesAsync(cancellationToken);
        if (_lifecycle.CanTransition(scenario.LifecycleStatus, AllocationScenarioLifecycle.Saved))
        {
            await _lifecycle.TransitionTrackedAsync(
                scenario,
                AllocationScenarioLifecycle.Saved,
                AllocationAuditActions.Save,
                reason: $"Sandbox save: {item.Name}",
                createVersion: true,
                writeAudit: true,
                persist: true,
                cancellationToken: cancellationToken);
        }
        return Map(item);
    }

    public async Task<AllocationSandboxItem?> DuplicateAsync(Guid sandboxId, string? newName = null, CancellationToken cancellationToken = default)
    {
        var src = await _db.AllocationEngineSandboxItems.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SandboxId == sandboxId, cancellationToken);
        if (src is null) return null;
        var copy = new AllocationEngineSandboxItem
        {
            SandboxId = Guid.NewGuid(),
            Name = newName?.Trim() is { Length: > 0 } n ? n : $"{src.Name} (copy)",
            ScenarioId = src.ScenarioId,
            SessionId = src.SessionId,
            Tags = src.Tags,
            ScenarioJson = src.ScenarioJson,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(copy);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(copy);
    }

    public async Task<bool> ArchiveAsync(Guid sandboxId, CancellationToken cancellationToken = default)
    {
        var row = await _db.AllocationEngineSandboxItems
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SandboxId == sandboxId, cancellationToken);
        if (row is null) return false;
        row.IsArchived = true;
        row.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AllocationSandboxItem>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var q = _db.AllocationEngineSandboxItems.AsNoTracking().Where(s => s.TenantId == _currentUser.TenantId);
        if (!includeArchived) q = q.Where(s => !s.IsArchived);
        var rows = await q.OrderByDescending(s => s.CreatedDate).Take(100).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<AllocationScenario?> ReplayAsync(Guid sandboxId, CancellationToken cancellationToken = default)
    {
        var row = await _db.AllocationEngineSandboxItems.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SandboxId == sandboxId, cancellationToken);
        return row is null ? null : JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts);
    }

    private static AllocationSandboxItem Map(AllocationEngineSandboxItem s) => new()
    {
        SandboxId = s.SandboxId,
        Name = s.Name,
        ScenarioId = s.ScenarioId,
        SessionId = s.SessionId,
        SavedAt = s.CreatedDate,
        IsArchived = s.IsArchived,
        Tags = s.Tags,
    };
}

public sealed class AllocationDashboardService : IAllocationDashboardService
{
    private readonly IAllocationExecutionService _execution;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AllocationDashboardService(
        IAllocationExecutionService execution,
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _execution = execution;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AllocationDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var history = await _execution.GetHistoryAsync(cancellationToken);
        var scenarios = await _db.AllocationEngineScenarios.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .OrderByDescending(s => s.GeneratedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var best = scenarios.Select(s => s.TotalScore).DefaultIfEmpty(0).Max();
        var avgCap = 0.0;
        var compliance = 0.0;
        var distribution = new List<AllocationSectionSummary>();
        if (scenarios.Count > 0)
        {
            var latest = System.Text.Json.JsonSerializer.Deserialize<AllocationScenario>(scenarios[0].ScenarioJson);
            if (latest is not null)
            {
                distribution = latest.SectionSummaries.ToList();
                avgCap = distribution.Count == 0 ? 0 : distribution.Average(d => d.OccupancyPercent);
                var totalC = latest.Constraints.Count;
                compliance = totalC == 0 ? 100 : latest.Constraints.Count(c => c.Satisfied) * 100.0 / totalC;
            }
        }

        return new AllocationDashboardDto
        {
            TotalRuns = history.Count,
            BestScore = best,
            AverageCapacityUtilization = Math.Round(avgCap, 2),
            AverageConstraintCompliance = Math.Round(compliance, 2),
            RecentRuns = history.Take(10).ToList(),
            Distribution = distribution,
        };
    }
}
