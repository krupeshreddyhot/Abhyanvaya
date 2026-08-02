using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Dashboard;

public sealed class OptimizationDashboardDto
{
    public int TotalRuns { get; init; }
    public int CompletedRuns { get; init; }
    public int ApprovedRuns { get; init; }
    public decimal BestScore { get; init; }
    public decimal AverageImprovement { get; init; }
    public decimal AverageConflictReduction { get; init; }
    public decimal AverageFacultySatisfactionDelta { get; init; }
    public IReadOnlyList<StrategyUsageDto> TopStrategies { get; init; } = [];
    public IReadOnlyList<OptimizationRunSummaryDto> RecentRuns { get; init; } = [];
    public IReadOnlyList<ScenarioHistoryItemDto> ScenarioHistory { get; init; } = [];
}

public sealed class StrategyUsageDto
{
    public string StrategyCode { get; init; } = "";
    public int CandidateCount { get; init; }
}

public sealed class ScenarioHistoryItemDto
{
    public Guid? ScenarioId { get; init; }
    public Guid RunId { get; init; }
    public decimal ProjectedScore { get; init; }
    public decimal ImprovementDelta { get; init; }
    public DateTime StartedUtc { get; init; }
    public OptimizationEngineRunStatus Status { get; init; }
}

public interface IOptimizationDashboardService
{
    Task<OptimizationDashboardDto> GetAsync(int? academicYearId, int? departmentId, CancellationToken cancellationToken = default);
}

public sealed class OptimizationDashboardService : IOptimizationDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptimizationExecutionService _runs;

    public OptimizationDashboardService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IOptimizationExecutionService runs)
    {
        _db = db;
        _currentUser = currentUser;
        _runs = runs;
    }

    public async Task<OptimizationDashboardDto> GetAsync(
        int? academicYearId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var q = _db.SchedulingOptimizationEngineRuns.AsNoTracking()
            .Where(r => r.TenantId == _currentUser.TenantId && !r.IsDeleted);
        if (academicYearId.HasValue) q = q.Where(r => r.AcademicYearId == academicYearId.Value);
        if (departmentId.HasValue) q = q.Where(r => r.DepartmentId == departmentId.Value);

        var runs = await q.OrderByDescending(r => r.StartedUtc).Take(200).ToListAsync(cancellationToken);
        var completed = runs.Where(r =>
            r.Status is OptimizationEngineRunStatus.Completed
                or OptimizationEngineRunStatus.Approved
                or OptimizationEngineRunStatus.Rejected).ToList();

        var recent = await _runs.ListRunsAsync(academicYearId, departmentId, cancellationToken);

        return new OptimizationDashboardDto
        {
            TotalRuns = runs.Count,
            CompletedRuns = completed.Count,
            ApprovedRuns = runs.Count(r => r.Status == OptimizationEngineRunStatus.Approved),
            BestScore = completed.Select(r => r.ProjectedScore).DefaultIfEmpty(0).Max(),
            AverageImprovement = completed.Count == 0 ? 0 : Math.Round(completed.Average(r => r.ImprovementDelta), 2),
            AverageConflictReduction = completed.Count == 0
                ? 0
                : Math.Round(completed.Average(r => (decimal)(r.BaselineConflictCount - r.ProjectedConflictCount)), 2),
            AverageFacultySatisfactionDelta = completed.Count == 0
                ? 0
                : Math.Round(completed.Average(r => r.ImprovementDelta * 0.15m), 2),
            TopStrategies =
            [
                new StrategyUsageDto { StrategyCode = "GREEDY", CandidateCount = CountStrategy(completed, "GREEDY") },
                new StrategyUsageDto { StrategyCode = "WORKLOAD", CandidateCount = CountStrategy(completed, "WORKLOAD") },
                new StrategyUsageDto { StrategyCode = "ROOM", CandidateCount = CountStrategy(completed, "ROOM") },
                new StrategyUsageDto { StrategyCode = "PREFERENCE", CandidateCount = CountStrategy(completed, "PREFERENCE") },
            ],
            RecentRuns = recent.Take(20).ToList(),
            ScenarioHistory = runs.Take(30).Select(r => new ScenarioHistoryItemDto
            {
                ScenarioId = r.SandboxScenarioId,
                RunId = r.RunId,
                ProjectedScore = r.ProjectedScore,
                ImprovementDelta = r.ImprovementDelta,
                StartedUtc = r.StartedUtc,
                Status = r.Status
            }).ToList()
        };
    }

    private static int CountStrategy(IEnumerable<Domain.Entities.Scheduling.OptimizationEngineRun> runs, string code) =>
        runs.Sum(r => r.CandidatesJson.Contains(code, StringComparison.OrdinalIgnoreCase) ? 1 : 0);
}
