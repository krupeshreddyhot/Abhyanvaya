using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1C.5A — Single authoritative scenario lifecycle state machine.
/// Controllers and other services must not set LifecycleStatus directly.
/// Flow: Transition → Version → Audit (orchestrated by callers via this service).
/// </summary>
public interface IAllocationScenarioLifecycleService
{
    bool CanTransition(string? from, string to);
    IReadOnlyCollection<string> GetAllowedTransitions(string? from);

    Task<AllocationGovernanceResult> TransitionAsync(
        Guid scenarioId,
        string toLifecycle,
        string operation,
        string? reason = null,
        string? notes = null,
        bool createVersion = true,
        bool writeAudit = true,
        bool persist = true,
        CancellationToken cancellationToken = default);

    Task<AllocationGovernanceResult> TransitionTrackedAsync(
        AllocationEngineScenario scenario,
        string toLifecycle,
        string operation,
        string? reason = null,
        string? notes = null,
        bool createVersion = true,
        bool writeAudit = true,
        bool persist = true,
        CancellationToken cancellationToken = default);
}

public sealed class AllocationScenarioLifecycleService : IAllocationScenarioLifecycleService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAllocationScenarioVersionService _versions;
    private readonly IAllocationAuditService _audit;

    private static readonly Dictionary<string, HashSet<string>> Transitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [AllocationScenarioLifecycle.Draft] = new(StringComparer.OrdinalIgnoreCase)
            {
                AllocationScenarioLifecycle.Saved,
                AllocationScenarioLifecycle.Simulated,
                AllocationScenarioLifecycle.Compared,
                AllocationScenarioLifecycle.Reviewed,
                AllocationScenarioLifecycle.Approved,
                AllocationScenarioLifecycle.Rejected,
                AllocationScenarioLifecycle.Archived,
            },
            [AllocationScenarioLifecycle.Saved] = new(StringComparer.OrdinalIgnoreCase)
            {
                AllocationScenarioLifecycle.Simulated,
                AllocationScenarioLifecycle.Compared,
                AllocationScenarioLifecycle.Reviewed,
                AllocationScenarioLifecycle.Approved,
                AllocationScenarioLifecycle.Rejected,
                AllocationScenarioLifecycle.Archived,
                AllocationScenarioLifecycle.Saved,
            },
            [AllocationScenarioLifecycle.Simulated] = new(StringComparer.OrdinalIgnoreCase)
            {
                AllocationScenarioLifecycle.Compared,
                AllocationScenarioLifecycle.Reviewed,
                AllocationScenarioLifecycle.Approved,
                AllocationScenarioLifecycle.Saved,
                AllocationScenarioLifecycle.Rejected,
                AllocationScenarioLifecycle.Archived,
                AllocationScenarioLifecycle.Simulated,
            },
            [AllocationScenarioLifecycle.Compared] = new(StringComparer.OrdinalIgnoreCase)
            {
                AllocationScenarioLifecycle.Reviewed,
                AllocationScenarioLifecycle.Approved,
                AllocationScenarioLifecycle.Simulated,
                AllocationScenarioLifecycle.Rejected,
                AllocationScenarioLifecycle.Archived,
                AllocationScenarioLifecycle.Compared,
            },
            [AllocationScenarioLifecycle.Reviewed] = new(StringComparer.OrdinalIgnoreCase)
            {
                AllocationScenarioLifecycle.Approved,
                AllocationScenarioLifecycle.Rejected,
                AllocationScenarioLifecycle.Archived,
                AllocationScenarioLifecycle.Compared,
                AllocationScenarioLifecycle.Reviewed,
            },
            [AllocationScenarioLifecycle.Approved] = new(StringComparer.OrdinalIgnoreCase)
            {
                AllocationScenarioLifecycle.Archived,
            },
            [AllocationScenarioLifecycle.Rejected] = new(StringComparer.OrdinalIgnoreCase)
            {
                AllocationScenarioLifecycle.Reviewed,
                AllocationScenarioLifecycle.Archived,
                AllocationScenarioLifecycle.Saved,
            },
            [AllocationScenarioLifecycle.Archived] = new(StringComparer.OrdinalIgnoreCase),
        };

    public AllocationScenarioLifecycleService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAllocationScenarioVersionService versions,
        IAllocationAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _versions = versions;
        _audit = audit;
    }

    public bool CanTransition(string? from, string to)
    {
        var normalizedFrom = AllocationScenarioLifecycle.Normalize(from);
        var normalizedTo = AllocationScenarioLifecycle.Normalize(to);
        if (string.Equals(normalizedFrom, normalizedTo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return true;

        // Illegal: Archived → Draft, Approved → Draft, Rejected → Approved
        if (normalizedFrom == AllocationScenarioLifecycle.Archived) return false;
        if (normalizedFrom == AllocationScenarioLifecycle.Approved
            && normalizedTo == AllocationScenarioLifecycle.Draft) return false;
        if (normalizedFrom == AllocationScenarioLifecycle.Rejected
            && normalizedTo == AllocationScenarioLifecycle.Approved) return false;

        return Transitions.TryGetValue(normalizedFrom, out var allowed)
               && allowed.Contains(normalizedTo);
    }

    public IReadOnlyCollection<string> GetAllowedTransitions(string? from)
    {
        var normalized = AllocationScenarioLifecycle.Normalize(from);
        return Transitions.TryGetValue(normalized, out var allowed)
            ? allowed.OrderBy(x => x).ToList()
            : Array.Empty<string>();
    }

    public async Task<AllocationGovernanceResult> TransitionAsync(
        Guid scenarioId,
        string toLifecycle,
        string operation,
        string? reason = null,
        string? notes = null,
        bool createVersion = true,
        bool writeAudit = true,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _db.AllocationEngineScenarios
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken);
        if (scenario is null)
        {
            return AllocationGovernanceResult.Failure(operation, scenarioId, "Scenario not found.",
                errors: ["Scenario not found."]);
        }

        return await TransitionTrackedAsync(
            scenario, toLifecycle, operation, reason, notes, createVersion, writeAudit, persist, cancellationToken);
    }

    public async Task<AllocationGovernanceResult> TransitionTrackedAsync(
        AllocationEngineScenario scenario,
        string toLifecycle,
        string operation,
        string? reason = null,
        string? notes = null,
        bool createVersion = true,
        bool writeAudit = true,
        bool persist = true,
        CancellationToken cancellationToken = default)
    {
        if (!CanTransition(scenario.LifecycleStatus, toLifecycle))
        {
            return AllocationGovernanceResult.Failure(
                operation,
                scenario.ScenarioId,
                $"Illegal lifecycle transition: {scenario.LifecycleStatus} → {toLifecycle}.",
                errors: [$"Illegal lifecycle transition: {scenario.LifecycleStatus} → {toLifecycle}."],
                version: scenario.CurrentVersionNumber);
        }

        // Same normalized state with no material change — skip meaningless version.
        var same = string.Equals(
            AllocationScenarioLifecycle.Normalize(scenario.LifecycleStatus),
            AllocationScenarioLifecycle.Normalize(toLifecycle),
            StringComparison.OrdinalIgnoreCase);
        var materialNotes = !string.IsNullOrWhiteSpace(notes)
                            && !string.Equals(scenario.ReviewNotes, notes, StringComparison.Ordinal);

        scenario.LifecycleStatus = toLifecycle;
        if (notes is not null)
            scenario.ReviewNotes = notes;
        scenario.UpdatedDate = DateTime.UtcNow;

        AllocationScenarioVersionDto? version = null;
        if (createVersion && (!same || materialNotes || NeedsVersion(operation)))
        {
            version = await _versions.AppendVersionAsync(
                scenario.ScenarioId,
                reason ?? operation,
                operation: operation,
                persist: false,
                cancellationToken: cancellationToken);
        }

        if (writeAudit)
        {
            await _audit.WriteAsync(
                operation,
                scenario.ScenarioId,
                scenario.SessionId,
                scenario.CurrentVersionNumber,
                scenario.ContextVersion,
                "Ok",
                reason ?? notes,
                persist: false,
                cancellationToken: cancellationToken);
        }

        if (persist)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return AllocationGovernanceResult.Failure(
                    operation,
                    scenario.ScenarioId,
                    AllocationConcurrencyMessages.ScenarioChanged,
                    errors: [AllocationConcurrencyMessages.ScenarioChanged],
                    concurrencyConflict: true,
                    version: scenario.CurrentVersionNumber);
            }
        }

        return new AllocationGovernanceResult
        {
            Success = true,
            Operation = operation,
            ScenarioId = scenario.ScenarioId,
            ScenarioVersion = scenario.CurrentVersionNumber,
            Message = $"{operation} succeeded.",
            CanApprove = toLifecycle != AllocationScenarioLifecycle.Approved
                         && toLifecycle != AllocationScenarioLifecycle.Archived,
            Warnings = [],
            Errors = [],
            BlockingReasons = [],
        };
    }

    private static bool NeedsVersion(string operation) =>
        operation is AllocationAuditActions.CreateScenario
            or AllocationAuditActions.Save
            or AllocationAuditActions.Review
            or AllocationAuditActions.Compare
            or AllocationAuditActions.Approve
            or AllocationAuditActions.Reject
            or AllocationAuditActions.Archive
            or AllocationAuditActions.Simulate
            or AllocationAuditActions.Replay;
}
