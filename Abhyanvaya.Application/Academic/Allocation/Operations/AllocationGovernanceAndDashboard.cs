using System.Text.Json;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class AllocationGovernanceService : IAllocationGovernanceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionAllocationContextBuilder _builder;
    private readonly IAllocationApprovalService _approval;
    private readonly IAllocationScenarioLifecycleService _lifecycle;
    private readonly IAcademicTelemetryService _telemetry;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationGovernanceService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionAllocationContextBuilder builder,
        IAllocationApprovalService approval,
        IAllocationScenarioLifecycleService lifecycle,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _builder = builder;
        _approval = approval;
        _lifecycle = lifecycle;
        _telemetry = telemetry;
    }

    public async Task<AllocationGovernanceResult> EvaluateAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var row = await _db.AllocationEngineScenarios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken)
            ?? throw new InvalidOperationException("Scenario not found.");

        var blockers = new List<string>();
        var warnings = new List<string>();
        var stale = false;
        var checksumInvalid = false;
        string? currentContextVersion = null;

        if (row.LifecycleStatus == AllocationScenarioLifecycle.Archived)
            blockers.Add("Scenario is archived.");
        if (row.LifecycleStatus == AllocationScenarioLifecycle.Approved)
            blockers.Add("Scenario is already approved.");
        if (row.CurrentVersionNumber <= 0)
            blockers.Add("Scenario version is invalid.");

        var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts);
        if (scenario is null)
            blockers.Add("Scenario payload invalid.");
        else
        {
            var expected = AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
            {
                ScenarioId = row.ScenarioId,
                VersionNumber = row.CurrentVersionNumber,
                ContextVersion = row.ContextVersion,
                ContextChecksum = row.ContextChecksum,
                StrategyConfigurationVersion = row.StrategyConfigurationVersion,
                ConstraintConfigurationVersion = row.ConstraintConfigurationVersion,
                LifecycleStatus = row.LifecycleStatus,
                Operation = "",
                Score = row.TotalScore,
                ScenarioJson = row.ScenarioJson,
                TraceJson = "[]",
                ConfigJson = "{}",
            });
            // Prefer stored version checksum when present; fall back to scenario payload integrity.
            if (!string.IsNullOrWhiteSpace(row.ScenarioChecksum))
            {
                var latest = await _db.AllocationScenarioVersions.AsNoTracking()
                    .Where(v => v.TenantId == _currentUser.TenantId && v.ScenarioId == scenarioId)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);
                if (latest is not null
                    && !string.Equals(latest.Checksum, row.ScenarioChecksum, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(row.ScenarioChecksum, AllocationCanonicalChecksum.Sha256Utf8(row.ScenarioJson), StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(row.ScenarioChecksum, expected, StringComparison.OrdinalIgnoreCase))
                {
                    checksumInvalid = true;
                    blockers.Add("Scenario checksum validation failed.");
                }
            }

            var mandatory = scenario.Constraints
                .Where(c => c.Priority == AllocationConstraintPriority.Mandatory && !c.Satisfied)
                .ToList();
            if (mandatory.Count > 0)
                blockers.Add($"Mandatory constraints unresolved: {string.Join(", ", mandatory.Select(m => m.ConstraintCode))}.");
        }

        if (row.AcademicYearId > 0 && row.CourseId > 0 && row.GroupId > 0 && row.SemesterId > 0)
        {
            try
            {
                var current = await _builder.BuildAsync(new AllocationScopeRequest
                {
                    AcademicYearId = row.AcademicYearId,
                    CourseId = row.CourseId,
                    GroupId = row.GroupId,
                    SemesterId = row.SemesterId,
                }, cancellationToken);
                currentContextVersion = current.ContextId.ToString("N")[..8];
                if (!string.Equals(current.Checksum, row.ContextChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    stale = true;
                    blockers.Add(
                        "This scenario was created using an earlier academic configuration and must be rebuilt before approval.");
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not verify current context: {ex.Message}");
            }
        }

        var canApprove = blockers.Count == 0;
        return new AllocationGovernanceResult
        {
            Success = true,
            Operation = "Evaluate",
            ScenarioId = scenarioId,
            ScenarioVersion = row.CurrentVersionNumber,
            Message = canApprove ? "Scenario is eligible for approval." : "Scenario cannot be approved.",
            CanApprove = canApprove,
            BlockingReasons = blockers,
            Warnings = warnings,
            Errors = canApprove ? [] : blockers,
            ContextStale = stale,
            ChecksumInvalid = checksumInvalid,
            ScenarioContextVersion = row.ContextVersion,
            CurrentContextVersion = currentContextVersion ?? row.ContextVersion,
            ContextCurrent = !stale,
        };
    }

    public Task<AllocationGovernanceResult> ApproveWithGovernanceAsync(Guid scenarioId, CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationApproval,
            "Allocation.GovernedApprove",
            ct => ApproveCoreAsync(scenarioId, ct),
            cancellationToken);

    private async Task<AllocationGovernanceResult> ApproveCoreAsync(Guid scenarioId, CancellationToken ct)
    {
        var gate = await EvaluateAsync(scenarioId, ct);
        if (!gate.CanApprove)
        {
            return AllocationGovernanceResult.Failure(
                AllocationAuditActions.Approve,
                scenarioId,
                string.Join(" ", gate.BlockingReasons),
                errors: gate.BlockingReasons.ToList(),
                contextStale: gate.ContextStale,
                checksumInvalid: gate.ChecksumInvalid,
                version: gate.ScenarioVersion);
        }

        AllocationDraft? draft = null;
        AllocationGovernanceResult? transition = null;
        try
        {
            await _db.ExecuteInTransactionAsync(async txCt =>
            {
                draft = await _approval.ApproveAsync(scenarioId, txCt, persist: false);
                transition = await _lifecycle.TransitionAsync(
                    scenarioId,
                    AllocationScenarioLifecycle.Approved,
                    AllocationAuditActions.Approve,
                    reason: "Approved — draft allocation created",
                    createVersion: true,
                    writeAudit: true,
                    persist: true,
                    cancellationToken: txCt);
                if (!transition.Success)
                    throw new InvalidOperationException(transition.Message);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AllocationGovernanceResult.Failure(
                AllocationAuditActions.Approve,
                scenarioId,
                AllocationConcurrencyMessages.ScenarioChanged,
                errors: [AllocationConcurrencyMessages.ScenarioChanged],
                concurrencyConflict: true);
        }
        catch (InvalidOperationException ex)
        {
            return AllocationGovernanceResult.Failure(
                AllocationAuditActions.Approve,
                scenarioId,
                ex.Message,
                errors: [ex.Message],
                contextStale: gate.ContextStale,
                checksumInvalid: gate.ChecksumInvalid);
        }

        return new AllocationGovernanceResult
        {
            Success = true,
            Operation = AllocationAuditActions.Approve,
            ScenarioId = scenarioId,
            ScenarioVersion = transition?.ScenarioVersion,
            Message = "Approved — draft allocation created. Live student allocations were not modified.",
            CanApprove = false,
            Draft = draft,
            Warnings = gate.Warnings,
            Errors = [],
            BlockingReasons = [],
            ContextStale = false,
            ContextCurrent = gate.ContextCurrent,
            ScenarioContextVersion = gate.ScenarioContextVersion,
            CurrentContextVersion = gate.CurrentContextVersion,
        };
    }

    public Task<AllocationGovernanceResult> RejectAsync(Guid scenarioId, string? reason = null, CancellationToken cancellationToken = default)
        => GovernedTransitionAsync(scenarioId, AllocationScenarioLifecycle.Rejected, AllocationAuditActions.Reject, reason, reason, cancellationToken);

    public Task<AllocationGovernanceResult> ReviewAsync(Guid scenarioId, string? notes = null, CancellationToken cancellationToken = default)
        => GovernedTransitionAsync(scenarioId, AllocationScenarioLifecycle.Reviewed, AllocationAuditActions.Review, notes, notes, cancellationToken);

    public Task<AllocationGovernanceResult> ArchiveAsync(Guid scenarioId, CancellationToken cancellationToken = default)
        => GovernedTransitionAsync(scenarioId, AllocationScenarioLifecycle.Archived, AllocationAuditActions.Archive, "Archived", null, cancellationToken);

    public Task<AllocationGovernanceResult> SaveAsync(Guid scenarioId, string? reason = null, CancellationToken cancellationToken = default)
        => GovernedTransitionAsync(scenarioId, AllocationScenarioLifecycle.Saved, AllocationAuditActions.Save, reason ?? "Saved", null, cancellationToken);

    public Task<AllocationGovernanceResult> MarkComparedAsync(Guid scenarioId, CancellationToken cancellationToken = default)
        => GovernedTransitionAsync(scenarioId, AllocationScenarioLifecycle.Compared, AllocationAuditActions.Compare, "Compared", null, cancellationToken);

    private async Task<AllocationGovernanceResult> GovernedTransitionAsync(
        Guid scenarioId,
        string toLifecycle,
        string operation,
        string? reason,
        string? notes,
        CancellationToken cancellationToken)
    {
        try
        {
            AllocationGovernanceResult? result = null;
            await _db.ExecuteInTransactionAsync(async ct =>
            {
                result = await _lifecycle.TransitionAsync(
                    scenarioId, toLifecycle, operation, reason, notes,
                    createVersion: true, writeAudit: true, persist: true, cancellationToken: ct);
                if (!result.Success)
                    throw new InvalidOperationException(result.Message);
            }, cancellationToken);
            return result!;
        }
        catch (DbUpdateConcurrencyException)
        {
            return AllocationGovernanceResult.Failure(
                operation,
                scenarioId,
                AllocationConcurrencyMessages.ScenarioChanged,
                errors: [AllocationConcurrencyMessages.ScenarioChanged],
                concurrencyConflict: true);
        }
        catch (InvalidOperationException ex)
        {
            return AllocationGovernanceResult.Failure(operation, scenarioId, ex.Message, errors: [ex.Message]);
        }
    }
}

public sealed class AllocationOpsDashboardService : IAllocationOpsDashboardService
{
    private readonly IAllocationHistoryService _history;
    private readonly IAllocationAnalyticsService _analytics;
    private readonly IAllocationAuditService _audit;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationOpsDashboardService(
        IAllocationHistoryService history,
        IAllocationAnalyticsService analytics,
        IAllocationAuditService audit,
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _history = history;
        _analytics = analytics;
        _audit = audit;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AllocationOpsDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var analytics = await _analytics.GetAsync("AcademicYear", cancellationToken);
        var recent = await _history.QueryAsync(new AllocationHistoryFilter(), cancellationToken);
        var activity = await _audit.ListAsync(20, cancellationToken);
        var scenarios = await _db.AllocationEngineScenarios.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .OrderByDescending(s => s.GeneratedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var policy = await _db.TenantSectionCapacityPolicies.AsNoTracking()
            .Where(p => p.TenantId == _currentUser.TenantId)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var warningPct = policy?.WarningPercent ?? 90;
        var underPct = policy?.UnderCapacityPercent ?? 40;

        var latest = scenarios.FirstOrDefault();
        var heatmap = BuildHeatmap(latest, warningPct, underPct);
        var constraints = BuildConstraints(latest);

        return new AllocationOpsDashboardDto
        {
            TotalRuns = analytics.TotalRuns,
            SuccessfulRuns = analytics.SuccessfulRuns,
            FailedRuns = analytics.FailedRuns,
            CancelledRuns = analytics.CancelledRuns,
            TimedOutRuns = analytics.TimedOutRuns,
            RunningRuns = analytics.RunningRuns,
            StudentsAllocated = analytics.StudentsAllocated,
            StudentsUnallocated = analytics.StudentsUnallocated,
            AverageScore = analytics.AverageScore,
            OverCapacitySections = heatmap.Cells.Count(c => c.Band == "OverCapacity"),
            NearCapacitySections = heatmap.Cells.Count(c => c.Band == "NearCapacity"),
            UnderUtilizedSections = heatmap.Cells.Count(c => c.Band == "Underused"),
            OptimalSections = heatmap.Cells.Count(c => c.Band == "Healthy"),
            MandatoryViolations = constraints.MandatoryViolations,
            PreferredWarnings = constraints.PreferredViolations,
            InformationalFindings = constraints.InformationalFindings,
            MandatoryCompliance = constraints.MandatoryCompliance,
            PreferredCompliance = constraints.PreferredCompliance,
            CompliancePercent = constraints.MandatoryCompliance,
            DraftCount = scenarios.Count(s =>
                AllocationScenarioLifecycle.Normalize(s.LifecycleStatus) == AllocationScenarioLifecycle.Draft),
            UnderReviewCount = scenarios.Count(s =>
            {
                var life = AllocationScenarioLifecycle.Normalize(s.LifecycleStatus);
                return life is AllocationScenarioLifecycle.Reviewed
                    or AllocationScenarioLifecycle.Compared
                    or AllocationScenarioLifecycle.Simulated;
            }),
            ApprovedCount = scenarios.Count(s => s.LifecycleStatus == AllocationScenarioLifecycle.Approved),
            RejectedCount = scenarios.Count(s => s.LifecycleStatus == AllocationScenarioLifecycle.Rejected),
            ArchivedCount = scenarios.Count(s => s.LifecycleStatus == AllocationScenarioLifecycle.Archived),
            RecentRuns = recent.Take(10).ToList(),
            RecentActivity = activity,
            Heatmap = heatmap,
            Constraints = constraints,
        };
    }

    private AllocationHeatmapDto BuildHeatmap(AllocationEngineScenario? row, int warningPercent, int underCapacityPercent)
    {
        if (row is null)
        {
            return new AllocationHeatmapDto
            {
                WarningPercent = warningPercent,
                UnderCapacityPercent = underCapacityPercent,
            };
        }

        var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts);
        if (scenario is null)
        {
            return new AllocationHeatmapDto
            {
                ScenarioId = row.ScenarioId,
                LifecycleStatus = row.LifecycleStatus,
                WarningPercent = warningPercent,
                UnderCapacityPercent = underCapacityPercent,
            };
        }

        var cells = scenario.SectionSummaries.Select(s =>
        {
            var overHard = s.MaximumCapacity > 0 && s.AssignedCount > s.MaximumCapacity;
            var band = overHard || s.OccupancyPercent > 100 ? "OverCapacity"
                : s.OccupancyPercent >= warningPercent ? "NearCapacity"
                : s.OccupancyPercent <= underCapacityPercent ? "Underused"
                : "Healthy";
            return new AllocationHeatmapCell
            {
                SectionId = s.SectionId,
                SectionCode = s.SectionCode,
                StudentCount = s.AssignedCount,
                MaximumCapacity = s.MaximumCapacity,
                AvailableCapacity = Math.Max(0, s.MaximumCapacity - s.AssignedCount - s.ReservedSeats),
                OccupancyPercent = s.OccupancyPercent,
                Band = band,
            };
        }).ToList();

        return new AllocationHeatmapDto
        {
            Title = "Latest Scenario – Section Utilization",
            ScopeNote = "Latest Scenario only — not Current Institutional Allocation / live production state.",
            ScenarioId = row.ScenarioId,
            LifecycleStatus = row.LifecycleStatus,
            Cells = cells,
            AverageOccupancy = cells.Count == 0 ? 0 : Math.Round(cells.Average(c => c.OccupancyPercent), 2),
            WarningPercent = warningPercent,
            UnderCapacityPercent = underCapacityPercent,
        };
    }

    private static AllocationConstraintDashboardDto BuildConstraints(AllocationEngineScenario? row)
    {
        if (row is null) return new AllocationConstraintDashboardDto();
        var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts);
        if (scenario is null) return new AllocationConstraintDashboardDto();
        var rows = scenario.Constraints;
        var mandatoryRows = rows.Where(c => c.Priority == AllocationConstraintPriority.Mandatory).ToList();
        var preferredRows = rows.Where(c => c.Priority == AllocationConstraintPriority.Preferred).ToList();
        var info = rows.Count(c => c.Priority == AllocationConstraintPriority.Informational && !c.Satisfied);
        var mandatoryViolations = mandatoryRows.Count(c => !c.Satisfied);
        var preferredViolations = preferredRows.Count(c => !c.Satisfied);
        var mandatoryCompliance = mandatoryRows.Count == 0
            ? 100
            : Math.Round(mandatoryRows.Count(c => c.Satisfied) * 100.0 / mandatoryRows.Count, 2);
        var preferredCompliance = preferredRows.Count == 0
            ? 100
            : Math.Round(preferredRows.Count(c => c.Satisfied) * 100.0 / preferredRows.Count, 2);
        return new AllocationConstraintDashboardDto
        {
            TotalConstraints = rows.Count,
            MandatoryViolations = mandatoryViolations,
            PreferredViolations = preferredViolations,
            InformationalFindings = info,
            MandatoryCompliance = mandatoryCompliance,
            PreferredCompliance = preferredCompliance,
            CompliancePercent = mandatoryCompliance,
            Rows = rows,
        };
    }
}
