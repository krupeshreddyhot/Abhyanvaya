using System.Diagnostics;
using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization.Metrics;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Application.Scheduling.Optimization.Telemetry;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Simulation;

public interface IOptimizationSimulationService
{
    Task<OptimizationSimulationDto> SimulateAsync(RunOptimizationSimulationRequest request, CancellationToken cancellationToken = default);
    Task<OptimizationSimulationDto?> GetAsync(Guid simulationId, CancellationToken cancellationToken = default);
    Task<SimulationComparisonDto> CompareAsync(CompareSimulationsRequest request, CancellationToken cancellationToken = default);
    Task<OptimizationSimulationDto> RejectAsync(RejectSimulationRequest request, CancellationToken cancellationToken = default);
    Task<OptimizationSimulationDto> AcceptAsync(AcceptSimulationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simulation-only engine. Produces preview/score/compare/reject/accept states.
/// Accept does NOT apply timetable changes in Phase 2B.6.
/// </summary>
public sealed class OptimizationSimulationService : IOptimizationSimulationService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IConflictDetectionService _conflicts;
    private readonly IOptimizationScoreCalculator _scoreCalculator;
    private readonly IEnumerable<IOptimizationStrategy> _strategies;
    private readonly IOptimizationMetricsService _metrics;
    private readonly IOptimizationTelemetryService _telemetry;

    public OptimizationSimulationService(
        IApplicationDbContext db,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IConflictDetectionService conflicts,
        IOptimizationScoreCalculator scoreCalculator,
        IEnumerable<IOptimizationStrategy> strategies,
        IOptimizationMetricsService metrics,
        IOptimizationTelemetryService telemetry)
    {
        _db = db;
        _uow = uow;
        _currentUser = currentUser;
        _conflicts = conflicts;
        _scoreCalculator = scoreCalculator;
        _strategies = strategies;
        _metrics = metrics;
        _telemetry = telemetry;
    }

    public async Task<OptimizationSimulationDto> SimulateAsync(
        RunOptimizationSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var academicYearId = request.AcademicYearId
            ?? await ResolveAcademicYearIdAsync(request.TimetableId, cancellationToken)
            ?? throw new InvalidOperationException("Academic year is required for simulation.");

        var workspace = await _conflicts.GetWorkspaceAsync(new ConflictWorkspaceQuery
        {
            AcademicYearId = academicYearId,
            TimetableId = request.TimetableId,
            DepartmentId = request.DepartmentId,
            UseLatestRun = true
        }, cancellationToken);

        var entryCount = request.TimetableId.HasValue
            ? await _db.SchedulingTimetableEntries.CountAsync(
                e => e.TenantId == _currentUser.TenantId && e.TimetableId == request.TimetableId.Value && !e.IsDeleted,
                cancellationToken)
            : await _db.SchedulingTimetableEntries.CountAsync(
                e => e.TenantId == _currentUser.TenantId && !e.IsDeleted, cancellationToken);

        var context = new OptimizationContext
        {
            TenantId = _currentUser.TenantId,
            AcademicYearId = academicYearId,
            TimetableId = request.TimetableId,
            DepartmentId = request.DepartmentId,
            EntryCount = entryCount,
            ConflictCount = workspace.Summary.TotalConflicts,
            BaselineMetrics = new Dictionary<string, decimal>()
        };

        var scoreSw = Stopwatch.StartNew();
        var baseline = _scoreCalculator.Calculate(context);
        scoreSw.Stop();

        // Phase 2B.6: projected score equals baseline (no optimizer). Strategy may only propose advisory candidates.
        var strategy = ResolveStrategy(request.StrategyKind);
        var propose = await strategy.ProposeAsync(context, new OptimizationRequest
        {
            TimetableId = request.TimetableId,
            AcademicYearId = academicYearId,
            DepartmentId = request.DepartmentId,
            StrategyKind = request.StrategyKind,
            ScenarioName = request.ScenarioName,
            PreviewOnly = true
        }, cancellationToken);

        var projected = baseline.Score; // immutable preview — no improvement invented by fake optimizer
        var metricDtos = await _metrics.CaptureAsync(academicYearId, request.TimetableId, request.DepartmentId, context, cancellationToken);
        foreach (var m in metricDtos)
            _telemetry.RecordMetricUsage(m.MetricName);

        sw.Stop();
        var simulationId = Guid.NewGuid();
        var run = new OptimizationSimulationRun
        {
            TenantId = _currentUser.TenantId,
            SimulationId = simulationId,
            TimetableId = request.TimetableId,
            AcademicYearId = academicYearId,
            DepartmentId = request.DepartmentId,
            StrategyKind = request.StrategyKind,
            Status = OptimizationSimulationStatus.Previewed,
            ScenarioName = request.ScenarioName ?? $"Preview-{request.StrategyKind}",
            CurrentScore = baseline.Score.NormalizedScore,
            ProjectedScore = projected.NormalizedScore,
            ScoreDelta = 0,
            CurrentConflictCount = workspace.Summary.TotalConflicts,
            ProjectedConflictCount = workspace.Summary.TotalConflicts,
            ScoringTimeMs = scoreSw.ElapsedMilliseconds,
            ExecutionTimeMs = sw.ElapsedMilliseconds,
            MetricsJson = JsonSerializer.Serialize(metricDtos),
            ProposedChangesJson = "[]",
            StartedUtc = DateTime.UtcNow.AddMilliseconds(-sw.ElapsedMilliseconds),
            CompletedUtc = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        await _db.AddAsync(run);
        await _uow.SaveChangesAsync(cancellationToken);

        _telemetry.RecordSimulation(sw.ElapsedMilliseconds, scoreSw.ElapsedMilliseconds, 0);

        return Map(run, baseline.Score, projected, metricDtos, propose.Candidates,
            "Simulation preview complete. No timetable changes applied. No Apply path in Phase 2B.6.");
    }

    public async Task<OptimizationSimulationDto?> GetAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        var run = await _db.SchedulingOptimizationSimulationRuns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == _currentUser.TenantId && x.SimulationId == simulationId && !x.IsDeleted, cancellationToken);
        if (run is null) return null;

        var metrics = JsonSerializer.Deserialize<List<OptimizationMetricDto>>(run.MetricsJson) ?? [];
        var score = new OptimizationScore
        {
            TotalScore = run.CurrentScore,
            NormalizedScore = run.CurrentScore,
            Dimensions = []
        };
        var projected = new OptimizationScore
        {
            TotalScore = run.ProjectedScore,
            NormalizedScore = run.ProjectedScore,
            Dimensions = []
        };
        return Map(run, score, projected, metrics, [], run.Status.ToString());
    }

    public async Task<SimulationComparisonDto> CompareAsync(CompareSimulationsRequest request, CancellationToken cancellationToken = default)
    {
        var left = await GetAsync(request.LeftSimulationId, cancellationToken)
            ?? throw new KeyNotFoundException("Left simulation not found.");
        var right = await GetAsync(request.RightSimulationId, cancellationToken)
            ?? throw new KeyNotFoundException("Right simulation not found.");

        await MarkStatusAsync(request.LeftSimulationId, OptimizationSimulationStatus.Compared, cancellationToken);
        await MarkStatusAsync(request.RightSimulationId, OptimizationSimulationStatus.Compared, cancellationToken);

        return new SimulationComparisonDto
        {
            Left = left,
            Right = right,
            ScoreDelta = right.ProjectedScore - left.ProjectedScore,
            ConflictDelta = right.ProjectedConflictCount - left.ProjectedConflictCount,
            Recommendation = "Comparison only — reject or accept for future apply pipeline. No timetable mutation."
        };
    }

    public async Task<OptimizationSimulationDto> RejectAsync(RejectSimulationRequest request, CancellationToken cancellationToken = default)
    {
        var run = await LoadTrackedAsync(request.SimulationId, cancellationToken);
        run.Status = OptimizationSimulationStatus.Rejected;
        run.RejectionReason = request.Reason;
        run.UpdatedDate = DateTime.UtcNow;
        run.UpdatedBy = _currentUser.UserId;
        await _uow.SaveChangesAsync(cancellationToken);
        _telemetry.RecordRejected();
        return (await GetAsync(request.SimulationId, cancellationToken))!;
    }

    public async Task<OptimizationSimulationDto> AcceptAsync(AcceptSimulationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.AcknowledgePreviewOnly)
            throw new InvalidOperationException("Accept requires AcknowledgePreviewOnly=true. Phase 2B.6 never applies timetable changes.");

        var run = await LoadTrackedAsync(request.SimulationId, cancellationToken);
        run.Status = OptimizationSimulationStatus.Accepted;
        run.UpdatedDate = DateTime.UtcNow;
        run.UpdatedBy = _currentUser.UserId;
        await _uow.SaveChangesAsync(cancellationToken);
        _telemetry.RecordAccepted();
        // Explicit: no timetable write path exists here.
        return (await GetAsync(request.SimulationId, cancellationToken))!;
    }

    private IOptimizationStrategy ResolveStrategy(OptimizationStrategyKind kind)
    {
        return _strategies.FirstOrDefault(s => s.Kind == kind)
            ?? _strategies.FirstOrDefault(s => s.Kind == OptimizationStrategyKind.None)
            ?? new NoOpOptimizationStrategy();
    }

    private async Task<int?> ResolveAcademicYearIdAsync(int? timetableId, CancellationToken cancellationToken)
    {
        if (!timetableId.HasValue) return null;
        return await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.Id == timetableId.Value && t.TenantId == _currentUser.TenantId)
            .Select(t => (int?)t.AcademicYearId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<OptimizationSimulationRun> LoadTrackedAsync(Guid simulationId, CancellationToken cancellationToken)
    {
        return await _db.SchedulingOptimizationSimulationRuns
            .FirstOrDefaultAsync(x => x.TenantId == _currentUser.TenantId && x.SimulationId == simulationId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Simulation not found.");
    }

    private async Task MarkStatusAsync(Guid simulationId, OptimizationSimulationStatus status, CancellationToken cancellationToken)
    {
        var run = await _db.SchedulingOptimizationSimulationRuns
            .FirstOrDefaultAsync(x => x.TenantId == _currentUser.TenantId && x.SimulationId == simulationId && !x.IsDeleted, cancellationToken);
        if (run is null) return;
        run.Status = status;
        run.UpdatedDate = DateTime.UtcNow;
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static OptimizationSimulationDto Map(
        OptimizationSimulationRun run,
        OptimizationScore baseline,
        OptimizationScore projected,
        IReadOnlyList<OptimizationMetricDto> metrics,
        IReadOnlyList<OptimizationCandidate> candidates,
        string message) =>
        new()
        {
            SimulationId = run.SimulationId,
            ScenarioName = run.ScenarioName,
            StrategyKind = run.StrategyKind,
            Status = run.Status,
            CurrentScore = run.CurrentScore,
            ProjectedScore = run.ProjectedScore,
            ScoreDelta = run.ScoreDelta,
            CurrentConflictCount = run.CurrentConflictCount,
            ProjectedConflictCount = run.ProjectedConflictCount,
            BaselineScore = MapScore(baseline),
            ProjectedScoreDetail = MapScore(projected),
            Metrics = metrics,
            Candidates = candidates.Select(c => new OptimizationCandidateDto
            {
                CandidateId = c.CandidateId,
                Description = c.Description,
                ProposedChangeSummaries = c.ProposedChangeSummaries,
                IsAdvisoryOnly = true,
                ModifiesLiveTimetable = false
            }).ToList(),
            ProposedChanges = [],
            CanApply = false,
            ModifiesTimetable = false,
            Message = message,
            ScoringTimeMs = run.ScoringTimeMs,
            ExecutionTimeMs = run.ExecutionTimeMs
        };

    private static OptimizationScoreDto MapScore(OptimizationScore score) => new()
    {
        TotalScore = score.TotalScore,
        NormalizedScore = score.NormalizedScore,
        Dimensions = score.Dimensions.Select(d => new OptimizationDimensionScoreDto
        {
            Dimension = d.Dimension,
            DimensionName = d.Dimension.ToString(),
            RawValue = d.RawValue,
            Weight = d.Weight,
            WeightedScore = d.WeightedScore
        }).ToList()
    };
}
