namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1C — Pipeline strategy with single responsibility. Operates on working state only.</summary>
public interface IAllocationPipelineStrategy
{
    string StrategyCode { get; }
    string DisplayName { get; }
    int Order { get; }
    Task ApplyAsync(AllocationWorkingState state, CancellationToken cancellationToken = default);
}

public interface IStudentGroupingStrategy
{
    IReadOnlyList<int> OrderStudents(SectionAllocationContext context, string groupingMode);
}

public interface IAllocationConstraintEngine
{
    Task<IReadOnlyList<AllocationConstraintEvaluation>> EvaluateAsync(
        SectionAllocationContext context,
        AllocationScenario scenario,
        AllocationPipelineConfig config,
        CancellationToken cancellationToken = default);
}

public interface IAllocationScoreCalculator
{
    AllocationScoreBreakdown Score(SectionAllocationContext context, AllocationScenario scenario);
}

public interface IAllocationProgressPublisher
{
    Task PublishProgressAsync(int tenantId, AllocationProgress progress, CancellationToken cancellationToken = default);
    Task PublishCompletedAsync(int tenantId, AllocationExecutionResult result, CancellationToken cancellationToken = default);
    Task PublishFailedAsync(int tenantId, Guid sessionId, string message, CancellationToken cancellationToken = default);
}

/// <summary>Null progress publisher for tests / non-SignalR hosts.</summary>
public sealed class NullAllocationProgressPublisher : IAllocationProgressPublisher
{
    public Task PublishProgressAsync(int tenantId, AllocationProgress progress, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task PublishCompletedAsync(int tenantId, AllocationExecutionResult result, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    public Task PublishFailedAsync(int tenantId, Guid sessionId, string message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
