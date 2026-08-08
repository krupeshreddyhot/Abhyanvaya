namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>
/// AI29.1C — Allocation Engine. MUST only consume <see cref="SectionAllocationContext"/>.
/// Never queries repositories, capacity services, or operational services.
/// Produces scenarios only — never commits student allocations.
/// </summary>
public interface IAllocationEngine
{
    string EngineCode { get; }

    Task<AllocationExecutionResult> ExecuteAsync(
        AllocationExecutionContext execution,
        IProgress<AllocationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
