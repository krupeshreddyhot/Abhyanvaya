using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization.Metrics;
using Abhyanvaya.Application.Scheduling.Optimization.Plugins;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Application.Scheduling.Optimization.Simulation;
using Abhyanvaya.Application.Scheduling.Optimization.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization;

public interface IOptimizationReadinessService
{
    Task<OptimizationPreviewDto> GetPreviewAsync(Guid? simulationId, int? academicYearId, int? timetableId, CancellationToken cancellationToken = default);
    Task<OptimizationScoreDto> ScoreAsync(int? academicYearId, int? timetableId, int? departmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OptimizationMetricDto>> GetMetricsAsync(int academicYearId, int? timetableId, CancellationToken cancellationToken = default);
    Task<OptimizationTelemetryDto> GetTelemetryAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<OptimizationPluginDto> GetPlugins();
}

public sealed class OptimizationReadinessService : IOptimizationReadinessService
{
    private readonly IOptimizationSimulationService _simulation;
    private readonly IOptimizationScoreCalculator _scoreCalculator;
    private readonly IOptimizationMetricsService _metrics;
    private readonly IOptimizationTelemetryService _telemetry;
    private readonly IOptimizationPluginRegistry _plugins;
    private readonly IConflictDetectionService _conflicts;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public OptimizationReadinessService(
        IOptimizationSimulationService simulation,
        IOptimizationScoreCalculator scoreCalculator,
        IOptimizationMetricsService metrics,
        IOptimizationTelemetryService telemetry,
        IOptimizationPluginRegistry plugins,
        IConflictDetectionService conflicts,
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _simulation = simulation;
        _scoreCalculator = scoreCalculator;
        _metrics = metrics;
        _telemetry = telemetry;
        _plugins = plugins;
        _conflicts = conflicts;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OptimizationPreviewDto> GetPreviewAsync(
        Guid? simulationId,
        int? academicYearId,
        int? timetableId,
        CancellationToken cancellationToken = default)
    {
        OptimizationSimulationDto simulation;
        if (simulationId.HasValue)
        {
            simulation = await _simulation.GetAsync(simulationId.Value, cancellationToken)
                ?? await _simulation.SimulateAsync(new RunOptimizationSimulationRequest
                {
                    AcademicYearId = academicYearId,
                    TimetableId = timetableId
                }, cancellationToken);
        }
        else
        {
            simulation = await _simulation.SimulateAsync(new RunOptimizationSimulationRequest
            {
                AcademicYearId = academicYearId,
                TimetableId = timetableId,
                ScenarioName = "Preview"
            }, cancellationToken);
        }

        var ay = academicYearId ?? await ResolveAcademicYearIdAsync(timetableId, cancellationToken) ?? 0;
        ConflictDashboardDto? dashboard = null;
        IReadOnlyList<HeatMapDto> heatMaps = [];
        if (ay > 0)
        {
            dashboard = await _conflicts.GetDashboardAsync(ay, timetableId, cancellationToken);
            heatMaps = dashboard.HeatMaps;
        }

        return new OptimizationPreviewDto
        {
            Simulation = simulation,
            ConflictSnapshot = dashboard,
            HeatMaps = heatMaps,
            Telemetry = await _telemetry.GetSnapshotAsync(cancellationToken)
        };
    }

    public async Task<OptimizationScoreDto> ScoreAsync(
        int? academicYearId,
        int? timetableId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var ay = academicYearId ?? await ResolveAcademicYearIdAsync(timetableId, cancellationToken)
            ?? throw new InvalidOperationException("Academic year is required.");
        var workspace = await _conflicts.GetWorkspaceAsync(new ConflictWorkspaceQuery
        {
            AcademicYearId = ay,
            TimetableId = timetableId,
            DepartmentId = departmentId,
            UseLatestRun = true
        }, cancellationToken);

        var entryCount = await CountEntriesAsync(timetableId, cancellationToken);
        var summary = _scoreCalculator.Calculate(new OptimizationContext
        {
            TenantId = _currentUser.TenantId,
            AcademicYearId = ay,
            TimetableId = timetableId,
            DepartmentId = departmentId,
            EntryCount = entryCount,
            ConflictCount = workspace.Summary.TotalConflicts
        });

        return new OptimizationScoreDto
        {
            TotalScore = summary.Score.TotalScore,
            NormalizedScore = summary.Score.NormalizedScore,
            Dimensions = summary.Score.Dimensions.Select(d => new OptimizationDimensionScoreDto
            {
                Dimension = d.Dimension,
                DimensionName = d.Dimension.ToString(),
                RawValue = d.RawValue,
                Weight = d.Weight,
                WeightedScore = d.WeightedScore
            }).ToList()
        };
    }

    public Task<IReadOnlyList<OptimizationMetricDto>> GetMetricsAsync(
        int academicYearId,
        int? timetableId,
        CancellationToken cancellationToken = default) =>
        _metrics.ListLatestAsync(academicYearId, timetableId, cancellationToken);

    public Task<OptimizationTelemetryDto> GetTelemetryAsync(CancellationToken cancellationToken = default) =>
        _telemetry.GetSnapshotAsync(cancellationToken);

    public IReadOnlyList<OptimizationPluginDto> GetPlugins() =>
        _plugins.ListPlugins().Select(p => new OptimizationPluginDto
        {
            Category = p.Category,
            ProviderCode = p.ProviderCode,
            ProviderName = p.ProviderName,
            IsImplemented = p.IsImplemented,
            Notes = p.Notes
        }).ToList();

    private async Task<int?> ResolveAcademicYearIdAsync(int? timetableId, CancellationToken cancellationToken)
    {
        if (!timetableId.HasValue) return null;
        return await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.Id == timetableId.Value && t.TenantId == _currentUser.TenantId)
            .Select(t => (int?)t.AcademicYearId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<int> CountEntriesAsync(int? timetableId, CancellationToken cancellationToken)
    {
        var q = _db.SchedulingTimetableEntries.Where(e => e.TenantId == _currentUser.TenantId && !e.IsDeleted);
        if (timetableId.HasValue)
            q = q.Where(e => e.TimetableId == timetableId.Value);
        return q.CountAsync(cancellationToken);
    }
}
