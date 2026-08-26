using System.Diagnostics;

namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1C — Deterministic allocation engine.
/// Consumes only <see cref="SectionAllocationContext"/> (+ config/strategies injected at construction).
/// </summary>
public sealed class AllocationEngine : IAllocationEngine
{
    private readonly IStudentGroupingStrategy _grouping;
    private readonly AllocationPipeline _pipeline;
    private readonly IAllocationScoreCalculator _scorer;

    public AllocationEngine(
        IStudentGroupingStrategy grouping,
        IEnumerable<IAllocationPipelineStrategy> strategies,
        IAllocationScoreCalculator scorer)
    {
        _grouping = grouping;
        _pipeline = new AllocationPipeline(strategies);
        _scorer = scorer;
    }

    public string EngineCode => "AI29.1C";

    public async Task<AllocationExecutionResult> ExecuteAsync(
        AllocationExecutionContext execution,
        IProgress<AllocationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var sessionId = execution.SessionId == Guid.Empty ? Guid.NewGuid() : execution.SessionId;
        var sourceContext = execution.Context ?? throw new ArgumentNullException(nameof(execution.Context));
        var config = (execution.Config ?? AllocationPipelineConfig.Default).Normalize();

        // AI29.1D — Validate + resolve population/target sections against Allocation Context only.
        var scopeSelection = AllocationScopeSelectionValidator.Validate(sourceContext, config);
        if (!scopeSelection.IsValid)
            throw new ArgumentException(string.Join(" ", scopeSelection.Errors));

        var context = AllocationContextScopeApplier.Apply(sourceContext, scopeSelection);

        var ordered = _grouping.OrderStudents(context, config.GroupingMode);
        var state = new AllocationWorkingState(context, config, ordered);

        progress?.Report(new AllocationProgress
        {
            SessionId = sessionId,
            CurrentStrategy = "Grouping",
            ProgressPercent = 5,
            StudentsProcessed = 0,
            TotalStudents = ordered.Count,
            Message = $"Ordered {ordered.Count} students via {config.GroupingMode} ({scopeSelection.PopulationSummary}; {scopeSelection.TargetSectionsSummary})",
        });

        await _pipeline.RunAsync(state, progress, sessionId, cancellationToken);

        var scenarioId = Guid.NewGuid();
        var scenario = AllocationScenarioFactory.FromWorkingState(state, sessionId, scenarioId);
        var metadata = new Dictionary<string, string>(scenario.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["PopulationMode"] = config.PopulationSelection?.Mode ?? AllocationPopulationModes.AllEligible,
            ["PopulationSummary"] = scopeSelection.PopulationSummary,
            ["TargetSectionsSummary"] = scopeSelection.TargetSectionsSummary,
            ["TargetSectionCount"] = scopeSelection.ResolvedSectionIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ScopedStudentCount"] = scopeSelection.ResolvedStudentIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        scenario = new AllocationScenario
        {
            ScenarioId = scenario.ScenarioId,
            SessionId = scenario.SessionId,
            ContextId = scenario.ContextId,
            ContextChecksum = scenario.ContextChecksum,
            GeneratedAt = scenario.GeneratedAt,
            Status = state.Errors.Count > 0 ? "Failed" : "Generated",
            Recommendations = scenario.Recommendations,
            SectionSummaries = scenario.SectionSummaries,
            Constraints = state.ConstraintEvals,
            Score = state.CurrentScore.TotalScore > 0 ? state.CurrentScore : _scorer.Score(context, scenario),
            Metadata = metadata,
        };

        var mandatoryFail = scenario.Constraints.Any(c =>
            c.Priority == AllocationConstraintPriority.Mandatory && !c.Satisfied);
        if (mandatoryFail)
            state.Errors.Add("Mandatory constraint violations present — scenario retained for review.");

        sw.Stop();
        return new AllocationExecutionResult
        {
            SessionId = sessionId,
            ScenarioId = scenario.ScenarioId,
            Succeeded = state.Errors.Count == 0,
            Status = state.Errors.Count == 0 ? "Completed" : "CompletedWithErrors",
            Scenario = scenario,
            Trace = new AllocationTrace
            {
                TraceId = Guid.NewGuid(),
                SessionId = sessionId,
                Steps = state.TraceSteps.OrderBy(s => s.Order).ToList(),
            },
            Score = scenario.Score,
            Errors = state.Errors,
            Warnings = state.Warnings,
            DurationMs = sw.Elapsed.TotalMilliseconds,
        };
    }
}
