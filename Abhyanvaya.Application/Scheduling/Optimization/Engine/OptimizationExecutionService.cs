using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization.Progress;
using Abhyanvaya.Application.Scheduling.Optimization.Sandbox;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Engine;

public sealed class OptimizationExecutionService : IOptimizationExecutionService
{
    private readonly IOptimizationEngine _engine;
    private readonly IOptimizationContextBuilder _contextBuilder;
    private readonly ISandboxService _sandbox;
    private readonly IOptimizationProgressPublisher _progress;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public OptimizationExecutionService(
        IOptimizationEngine engine,
        IOptimizationContextBuilder contextBuilder,
        ISandboxService sandbox,
        IOptimizationProgressPublisher progress,
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _engine = engine;
        _contextBuilder = contextBuilder;
        _sandbox = sandbox;
        _progress = progress;
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<OptimizationExecutionResult> RunPipelineAsync(
        OptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var academicYearId = request.AcademicYearId
            ?? await ResolveAcademicYearIdAsync(request.TimetableId, cancellationToken)
            ?? throw new InvalidOperationException("Academic year is required.");

        var session = new OptimizationSession
        {
            TenantId = _currentUser.TenantId,
            AcademicYearId = academicYearId,
            TimetableId = request.TimetableId,
            DepartmentId = request.DepartmentId
        };

        var sourceVersionId = await ResolveSourceVersionIdAsync(request.TimetableId, cancellationToken);

        var run = new OptimizationEngineRun
        {
            TenantId = _currentUser.TenantId,
            RunId = session.RunId,
            SessionId = session.SessionId,
            Status = OptimizationEngineRunStatus.Running,
            StrategyKind = OptimizationStrategyKind.Pipeline,
            AcademicYearId = academicYearId,
            DepartmentId = request.DepartmentId,
            TimetableId = request.TimetableId,
            SourceScheduleVersionId = sourceVersionId,
            StartedUtc = session.StartedUtc,
            CurrentStrategy = "Starting",
            ProgressPercent = 0,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(run);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var workingContext = await _contextBuilder.BuildAsync(
                academicYearId, request.TimetableId, request.DepartmentId, cancellationToken);

            var execContext = new OptimizationExecutionContext
            {
                Session = session,
                WorkingContext = workingContext,
                Request = request,
                ProgressCallback = progress =>
                {
                    run.CurrentStrategy = progress.CurrentStrategy;
                    run.ProgressPercent = progress.ProgressPercent;
                    run.ElapsedMs = progress.ElapsedMs;
                    run.EstimatedRemainingMs = progress.EstimatedRemainingMs;
                    _ = _progress.PublishProgressAsync(_currentUser.TenantId, progress, cancellationToken);
                }
            };

            var result = await _engine.ExecuteAsync(execContext, cancellationToken);

            var scenario = await _sandbox.CreateFromOptimizationAsync(new CreateOptimizationScenarioRequest
            {
                Name = string.IsNullOrWhiteSpace(request.ScenarioName)
                    ? $"Optimization {DateTime.UtcNow:yyyyMMdd-HHmm}"
                    : request.ScenarioName.Trim(),
                Description = "Enterprise optimization pipeline result (sandbox only).",
                AcademicYearId = academicYearId,
                DepartmentId = request.DepartmentId,
                TimetableId = request.TimetableId,
                Category = "Optimization",
                TagsCsv = "phase3,pipeline",
                BaselineScore = result.CombinedResult.BaselineScore.NormalizedScore,
                ProjectedScore = result.CombinedResult.ProjectedScore?.NormalizedScore ?? 0,
                ConflictCount = result.CombinedResult.Summary.ProjectedConflictCount,
                CandidatesJson = JsonSerializer.Serialize(result.CombinedResult.Candidates),
                ComparisonJson = JsonSerializer.Serialize(result.Comparison),
                IntermediateResultsJson = JsonSerializer.Serialize(result.IntermediateResults),
                MetricsJson = JsonSerializer.Serialize(execContext.WorkingContext.BaselineMetrics),
                RunId = result.RunId
            }, cancellationToken);

            run.Status = OptimizationEngineRunStatus.Completed;
            run.CompletedUtc = DateTime.UtcNow;
            run.ElapsedMs = result.ElapsedMs;
            run.ProgressPercent = 100;
            run.CurrentStrategy = "Completed";
            run.BaselineScore = result.CombinedResult.Summary.BaselineScore;
            run.ProjectedScore = result.CombinedResult.Summary.BestProjectedScore;
            run.ImprovementDelta = result.CombinedResult.Summary.ImprovementDelta;
            run.BaselineConflictCount = result.CombinedResult.Summary.BaselineConflictCount;
            run.ProjectedConflictCount = result.CombinedResult.Summary.ProjectedConflictCount;
            run.SandboxScenarioId = scenario.ScenarioId;
            run.CandidatesJson = JsonSerializer.Serialize(result.CombinedResult.Candidates);
            run.ComparisonJson = JsonSerializer.Serialize(result.Comparison);
            run.IntermediateResultsJson = JsonSerializer.Serialize(result.IntermediateResults);
            run.MetricsJson = JsonSerializer.Serialize(execContext.WorkingContext.BaselineMetrics);
            run.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await _progress.PublishCompletedAsync(_currentUser.TenantId, result.RunId, cancellationToken);

            result.SandboxScenarioId = scenario.ScenarioId;
            return result;
        }
        catch (Exception ex)
        {
            run.Status = OptimizationEngineRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedUtc = DateTime.UtcNow;
            run.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await _progress.PublishFailedAsync(_currentUser.TenantId, session.RunId, ex.Message, cancellationToken);
            throw;
        }
    }

    public async Task<OptimizationExecutionResult?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _db.SchedulingOptimizationEngineRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == _currentUser.TenantId && r.RunId == runId && !r.IsDeleted, cancellationToken);
        if (run is null) return null;

        var candidates = JsonSerializer.Deserialize<List<OptimizationCandidate>>(run.CandidatesJson) ?? [];
        var intermediate = JsonSerializer.Deserialize<List<OptimizationIntermediateResult>>(run.IntermediateResultsJson) ?? [];
        var comparison = JsonSerializer.Deserialize<OptimizationComparisonDto>(run.ComparisonJson);

        return new OptimizationExecutionResult
        {
            RunId = run.RunId,
            SessionId = run.SessionId,
            Status = run.Status,
            SandboxScenarioId = run.SandboxScenarioId,
            Comparison = comparison,
            IntermediateResults = intermediate,
            ElapsedMs = run.ElapsedMs,
            ErrorMessage = run.ErrorMessage,
            CombinedResult = new OptimizationResult
            {
                Execution = new OptimizationExecution
                {
                    ExecutionId = run.RunId,
                    StrategyKind = run.StrategyKind,
                    StartedUtc = run.StartedUtc,
                    CompletedUtc = run.CompletedUtc,
                    ExecutionTimeMs = run.ElapsedMs,
                    Outcome = run.Status.ToString()
                },
                Summary = new OptimizationSummary
                {
                    CandidateCount = candidates.Count,
                    BaselineScore = run.BaselineScore,
                    BestProjectedScore = run.ProjectedScore,
                    ImprovementDelta = run.ImprovementDelta,
                    BaselineConflictCount = run.BaselineConflictCount,
                    ProjectedConflictCount = run.ProjectedConflictCount,
                    StatusMessage = run.Status == OptimizationEngineRunStatus.Completed
                        ? "Completed — awaiting user approval for new draft version."
                        : run.ErrorMessage ?? run.Status.ToString()
                },
                BaselineScore = new OptimizationScore { NormalizedScore = run.BaselineScore, TotalScore = run.BaselineScore },
                ProjectedScore = new OptimizationScore { NormalizedScore = run.ProjectedScore, TotalScore = run.ProjectedScore },
                Candidates = candidates
            }
        };
    }

    public async Task<IReadOnlyList<OptimizationRunSummaryDto>> ListRunsAsync(
        int? academicYearId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var q = _db.SchedulingOptimizationEngineRuns.AsNoTracking()
            .Where(r => r.TenantId == _currentUser.TenantId && !r.IsDeleted);
        if (academicYearId.HasValue) q = q.Where(r => r.AcademicYearId == academicYearId.Value);
        if (departmentId.HasValue) q = q.Where(r => r.DepartmentId == departmentId.Value);

        return await q.OrderByDescending(r => r.StartedUtc)
            .Take(100)
            .Select(r => new OptimizationRunSummaryDto
            {
                RunId = r.RunId,
                SessionId = r.SessionId,
                Status = r.Status,
                StrategyKind = r.StrategyKind,
                AcademicYearId = r.AcademicYearId,
                TimetableId = r.TimetableId,
                BaselineScore = r.BaselineScore,
                ProjectedScore = r.ProjectedScore,
                ImprovementDelta = r.ImprovementDelta,
                BaselineConflictCount = r.BaselineConflictCount,
                ProjectedConflictCount = r.ProjectedConflictCount,
                SandboxScenarioId = r.SandboxScenarioId,
                ResultDraftScheduleVersionId = r.ResultDraftScheduleVersionId,
                StartedUtc = r.StartedUtc,
                CompletedUtc = r.CompletedUtc,
                ElapsedMs = r.ElapsedMs,
                ModifiesProductionTimetable = false
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<int?> ResolveAcademicYearIdAsync(int? timetableId, CancellationToken cancellationToken)
    {
        if (!timetableId.HasValue) return null;
        return await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.Id == timetableId.Value && t.TenantId == _currentUser.TenantId)
            .Select(t => (int?)t.AcademicYearId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> ResolveSourceVersionIdAsync(int? timetableId, CancellationToken cancellationToken)
    {
        if (!timetableId.HasValue) return null;
        return await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.Id == timetableId.Value && t.TenantId == _currentUser.TenantId)
            .Select(t => t.ScheduleVersionId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
