using System.Text.Json;
using Abhyanvaya.Application.Academic.Observability;
// System.Text.Json required for scenario analytics deserialization
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class AllocationReplayService : IAllocationReplayService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAllocationExecutionService _execution;
    private readonly IAllocationAuditService _audit;
    private readonly IAllocationScenarioLifecycleService _lifecycle;
    private readonly IAcademicTelemetryService _telemetry;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationReplayService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAllocationExecutionService execution,
        IAllocationAuditService audit,
        IAllocationScenarioLifecycleService lifecycle,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _execution = execution;
        _audit = audit;
        _lifecycle = lifecycle;
        _telemetry = telemetry;
    }

    public Task<AllocationExecutionResult> ReplayAsync(Guid scenarioId, CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationReplay,
            "Allocation.Replay",
            ct => ReplayCoreAsync(scenarioId, ct),
            cancellationToken);

    private async Task<AllocationExecutionResult> ReplayCoreAsync(Guid scenarioId, CancellationToken ct)
    {
        var historical = await _db.AllocationEngineScenarios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, ct)
            ?? throw new InvalidOperationException("Scenario not found.");

        var session = await _db.AllocationEngineSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SessionId == historical.SessionId, ct);

        var config = session is null
            ? AllocationPipelineConfig.Default
            : JsonSerializer.Deserialize<AllocationPipelineConfig>(session.ConfigJson, JsonOpts) ?? AllocationPipelineConfig.Default;

        var scope = new AllocationScopeRequest
        {
            AcademicYearId = historical.AcademicYearId > 0 ? historical.AcademicYearId : session?.AcademicYearId ?? 0,
            CourseId = historical.CourseId > 0 ? historical.CourseId : session?.CourseId ?? 0,
            GroupId = historical.GroupId > 0 ? historical.GroupId : session?.GroupId ?? 0,
            SemesterId = historical.SemesterId > 0 ? historical.SemesterId : session?.SemesterId ?? 0,
        };

        // Produces a NEW scenario — never mutates historical.
        var result = await _execution.RunAsync(scope, config, ct);

        var newRow = await _db.AllocationEngineScenarios
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == result.ScenarioId, ct);
        if (newRow is not null)
        {
            newRow.ParentScenarioId = scenarioId;
            newRow.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _lifecycle.TransitionTrackedAsync(
                newRow,
                AllocationScenarioLifecycle.Simulated,
                AllocationAuditActions.Replay,
                reason: $"Replay of {scenarioId:D}",
                createVersion: true,
                writeAudit: true,
                persist: true,
                cancellationToken: ct);
        }
        else
        {
            await _audit.WriteAsync(
                AllocationAuditActions.Replay,
                result.ScenarioId,
                result.SessionId,
                1,
                historical.ContextVersion,
                "Ok",
                $"Replayed from {scenarioId:D}",
                cancellationToken: ct);
        }

        return result;
    }
}

public sealed class AllocationComparisonService : IAllocationComparisonService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAllocationScoreCalculator _scorer;
    private readonly IAllocationAuditService _audit;
    private readonly IAllocationScenarioLifecycleService _lifecycle;
    private readonly IAcademicTelemetryService _telemetry;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationComparisonService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAllocationScoreCalculator scorer,
        IAllocationAuditService audit,
        IAllocationScenarioLifecycleService lifecycle,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _scorer = scorer;
        _audit = audit;
        _lifecycle = lifecycle;
        _telemetry = telemetry;
    }

    public Task<AllocationMultiCompareReport> CompareAsync(
        IReadOnlyList<Guid> scenarioIds,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationComparison,
            "Allocation.MultiCompare",
            ct => CompareCoreAsync(scenarioIds, ct),
            cancellationToken);

    private async Task<AllocationMultiCompareReport> CompareCoreAsync(IReadOnlyList<Guid> scenarioIds, CancellationToken ct)
    {
        if (scenarioIds.Count == 0) throw new ArgumentException("At least one scenarioId required.");
        var sides = new List<AllocationScenarioCompareSide>();
        double originalScore = 0;
        var labelIndex = 0;

        foreach (var id in scenarioIds.Distinct())
        {
            var row = await _db.AllocationEngineScenarios.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == id, ct)
                ?? throw new InvalidOperationException($"Scenario {id} not found.");
            var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts)
                ?? new AllocationScenario();

            // Reuse AI29.1C score calculator — never invent a second score.
            var breakdown = scenario.Score.TotalScore > 0
                ? scenario.Score
                : _scorer.Score(new SectionAllocationContext
                {
                    ContextId = scenario.ContextId,
                    Checksum = scenario.ContextChecksum,
                    Sections = scenario.SectionSummaries.Select(s => new AllocationSectionProjection
                    {
                        SectionId = s.SectionId,
                        SectionCode = s.SectionCode,
                    }).ToList(),
                    Capacities = scenario.SectionSummaries.Select(s => new AllocationCapacityProjection
                    {
                        SectionId = s.SectionId,
                        MaximumCapacity = s.MaximumCapacity,
                        CurrentStrength = s.AssignedCount,
                        OccupancyPercent = s.OccupancyPercent,
                        ReservedSeats = s.ReservedSeats,
                    }).ToList(),
                }, scenario);

            if (labelIndex == 0)
                originalScore = EstimateOriginalScore(scenario);

            var moved = scenario.Recommendations.Count(r => r.FromSectionId != r.ToSectionId);
            var sections = scenario.Recommendations.Select(r => r.ToSectionId).Distinct().Count();
            var assignedIds = scenario.Recommendations.Select(r => r.StudentId).ToHashSet();
            // Unallocated approximated when recommendations < section summary student expectation via metadata
            var unallocated = Math.Max(0, scenario.SectionSummaries.Sum(s => s.AssignedCount) == 0
                ? 0
                : 0); // recommendations are the allocated set; unallocated counted from warnings pattern
            unallocated = scenario.Metadata.TryGetValue("UnallocatedCount", out var u) && int.TryParse(u, out var n) ? n : 0;

            var mandatory = scenario.Constraints.Count(c => c.Priority == AllocationConstraintPriority.Mandatory && !c.Satisfied);
            var preferred = scenario.Constraints.Count(c => c.Priority == AllocationConstraintPriority.Preferred && !c.Satisfied);
            var totalC = Math.Max(1, scenario.Constraints.Count);
            var compliance = scenario.Constraints.Count(c => c.Satisfied) * 100.0 / totalC;
            var util = scenario.SectionSummaries.Count == 0 ? 0 : scenario.SectionSummaries.Average(s => s.OccupancyPercent);

            sides.Add(new AllocationScenarioCompareSide
            {
                ScenarioId = id,
                Label = $"Scenario {(char)('A' + labelIndex)}",
                Score = breakdown.TotalScore,
                StudentsMoved = moved,
                SectionsAffected = sections,
                UnallocatedStudents = unallocated,
                MandatoryViolations = mandatory,
                PreferredViolations = preferred,
                CapacityUtilization = Math.Round(util, 2),
                ConstraintCompliance = Math.Round(compliance, 2),
                ScoreBreakdown = breakdown,
            });
            labelIndex++;

            var entity = await _db.AllocationEngineScenarios
                .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == id, ct);
            if (entity is not null
                && _lifecycle.CanTransition(entity.LifecycleStatus, AllocationScenarioLifecycle.Compared))
            {
                await _lifecycle.TransitionTrackedAsync(
                    entity,
                    AllocationScenarioLifecycle.Compared,
                    AllocationAuditActions.Compare,
                    reason: "Compared",
                    createVersion: true,
                    writeAudit: false,
                    persist: false,
                    cancellationToken: ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        var best = sides.OrderByDescending(s => s.Score).First();
        await _audit.WriteAsync(AllocationAuditActions.Compare, best.ScenarioId, null, null, null, "Ok",
            $"Compared {sides.Count} scenarios", cancellationToken: ct);

        return new AllocationMultiCompareReport
        {
            OriginalScore = Math.Round(originalScore, 2),
            Scenarios = sides,
            BestScenarioId = best.ScenarioId,
            BestScenarioLabel = best.Label,
            ImprovementVsOriginal = Math.Round(best.Score - originalScore, 2),
            Summary = $"Best {best.Label} score {best.Score:0.##} (Δ {best.Score - originalScore:+0.##;-0.##}).",
        };
    }

    private static double EstimateOriginalScore(AllocationScenario scenario)
    {
        // Deterministic baseline from FromSection distribution occupancy balance proxy.
        var groups = scenario.Recommendations.GroupBy(r => r.FromSectionId ?? 0).ToList();
        if (groups.Count == 0) return 70;
        var counts = groups.Select(g => g.Count()).ToList();
        var spread = counts.Max() - counts.Min();
        return Math.Max(0, Math.Min(100, 85 - spread * 3));
    }
}

public sealed class AllocationExplanationService : IAllocationExplanationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAllocationScoreCalculator _scorer;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationExplanationService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAllocationScoreCalculator scorer)
    {
        _db = db;
        _currentUser = currentUser;
        _scorer = scorer;
    }

    public async Task<AllocationExplanationReport> ExplainAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var row = await _db.AllocationEngineScenarios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken)
            ?? throw new InvalidOperationException("Scenario not found.");
        var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts)
            ?? throw new InvalidOperationException("Invalid scenario JSON.");

        var assigned = scenario.Recommendations.Select(r => new AllocationStudentExplanation
        {
            StudentId = r.StudentId,
            StudentNumber = r.StudentNumber,
            StudentName = r.StudentName,
            RecommendedSectionCode = r.ToSectionCode,
            Assigned = true,
            Reasons = r.Explanations.Count > 0
                ? r.Explanations
                : ["✓ Allocated from deterministic strategy trace"],
        }).ToList();

        var unallocated = new List<AllocationStudentExplanation>();
        // Rejection explanations from scenario warnings embedded in metadata/constraints
        foreach (var c in scenario.Constraints.Where(c => !c.Satisfied && c.Priority == AllocationConstraintPriority.Mandatory))
        {
            unallocated.Add(new AllocationStudentExplanation
            {
                StudentId = 0,
                Assigned = false,
                Reasons = [$"✗ {c.Summary}"],
            });
        }

        var score = scenario.Score.TotalScore > 0 ? scenario.Score : _scorer.Score(new SectionAllocationContext
        {
            ContextId = scenario.ContextId,
            Checksum = scenario.ContextChecksum,
        }, scenario);

        return new AllocationExplanationReport
        {
            ScenarioId = scenarioId,
            Assigned = assigned,
            Unallocated = unallocated,
            Score = score,
            Constraints = scenario.Constraints,
        };
    }
}

public sealed class AllocationAnalyticsService : IAllocationAnalyticsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AllocationAnalyticsService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AllocationAnalyticsDto> GetAsync(string period = "AcademicYear", CancellationToken cancellationToken = default)
    {
        var since = period.ToLowerInvariant() switch
        {
            "daily" => DateTime.UtcNow.Date,
            "weekly" => DateTime.UtcNow.Date.AddDays(-7),
            "monthly" => DateTime.UtcNow.Date.AddMonths(-1),
            _ => DateTime.UtcNow.Date.AddYears(-1),
        };

        var scenarios = await _db.AllocationEngineScenarios.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.GeneratedAt >= since)
            .ToListAsync(cancellationToken);
        var sessions = await _db.AllocationEngineSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.CreatedDate >= since)
            .ToListAsync(cancellationToken);

        // AI29.1C.5A — actual execution status counts (not TotalRuns × SuccessRate).
        var successful = sessions.Count(s => AllocationExecutionStatus.IsSuccessful(s.Status));
        var failed = sessions.Count(s => AllocationExecutionStatus.IsFailed(s.Status));
        var cancelled = sessions.Count(s => s.Status == AllocationExecutionStatus.Cancelled);
        var timedOut = sessions.Count(s => s.Status == AllocationExecutionStatus.TimedOut);
        var running = sessions.Count(s => s.Status == AllocationExecutionStatus.Running);
        var avgScore = scenarios.Count == 0 ? 0 : scenarios.Average(s => s.TotalScore);
        var allocated = 0;
        var mandatoryOk = 0;
        var preferredOk = 0;
        var mandatoryTotal = 0;
        var preferredTotal = 0;
        var informationalFindings = 0;
        var occ = 0.0;
        var occN = 0;

        foreach (var s in scenarios)
        {
            try
            {
                var scenario = JsonSerializer.Deserialize<AllocationScenario>(s.ScenarioJson);
                if (scenario is null) continue;
                allocated += scenario.Recommendations.Count;
                foreach (var c in scenario.Constraints)
                {
                    if (c.Priority == AllocationConstraintPriority.Mandatory)
                    {
                        mandatoryTotal++;
                        if (c.Satisfied) mandatoryOk++;
                    }
                    else if (c.Priority == AllocationConstraintPriority.Preferred)
                    {
                        preferredTotal++;
                        if (c.Satisfied) preferredOk++;
                    }
                    else if (c.Priority == AllocationConstraintPriority.Informational && !c.Satisfied)
                    {
                        informationalFindings++;
                    }
                }
                if (scenario.SectionSummaries.Count > 0)
                {
                    occ += scenario.SectionSummaries.Average(x => x.OccupancyPercent);
                    occN++;
                }
            }
            catch { /* ignore corrupt rows */ }
        }

        return new AllocationAnalyticsDto
        {
            Period = period,
            TotalRuns = sessions.Count,
            SuccessfulRuns = successful,
            FailedRuns = failed,
            CancelledRuns = cancelled,
            TimedOutRuns = timedOut,
            RunningRuns = running,
            SuccessRate = sessions.Count == 0 ? 0 : Math.Round(successful * 100.0 / sessions.Count, 2),
            StudentsAllocated = allocated,
            StudentsUnallocated = 0,
            AverageSectionOccupancy = occN == 0 ? 0 : Math.Round(occ / occN, 2),
            MandatoryCompliance = mandatoryTotal == 0 ? 100 : Math.Round(mandatoryOk * 100.0 / mandatoryTotal, 2),
            PreferredCompliance = preferredTotal == 0 ? 100 : Math.Round(preferredOk * 100.0 / preferredTotal, 2),
            InformationalFindings = informationalFindings,
            AverageScore = Math.Round(avgScore, 2),
            AverageImprovement = 0,
            AverageDurationMs = 0,
        };
    }
}
