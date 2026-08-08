namespace Abhyanvaya.Application.Academic.Allocation;

public interface ISectionAllocationContextBuilder
{
    Task<SectionAllocationContext> BuildAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task<SectionAllocationContext> RefreshAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task<AllocationSnapshotDto> SnapshotAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task<AllocationValidationReport> ValidateAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task<SectionAllocationAnalysisContext> BuildAnalysisContextAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task<AllocationContextCompositionReport?> GetLastCompositionReportAsync(CancellationToken cancellationToken = default);
}

public interface IAllocationReadinessService
{
    Task<AllocationReadinessReport> EvaluateAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
}

public interface ISectionAllocationContextValidator
{
    Task<AllocationValidationReport> ValidateAsync(SectionAllocationContext context, CancellationToken cancellationToken = default);
}

public interface IAllocationHealthService
{
    Task<AllocationHealthReport> EvaluateAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
}

public interface IAllocationContextCache
{
    Task WarmAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task SetAsync(AllocationScopeRequest scope, SectionAllocationContext context, CancellationToken cancellationToken = default);
    Task RefreshAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task InvalidateAsync(AllocationScopeRequest? scope = null, CancellationToken cancellationToken = default);
    Task<SectionAllocationContext?> GetAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(AllocationScopeRequest scope, CancellationToken cancellationToken = default);
}

public interface IAllocationSnapshotService
{
    Task<AllocationSnapshotDto> CreateAsync(SectionAllocationContext context, AllocationScopeRequest scope, CancellationToken cancellationToken = default);
    Task<AllocationSnapshotDto?> GetAsync(Guid snapshotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AllocationSnapshotDto>> ListAsync(AllocationScopeRequest? scope = null, CancellationToken cancellationToken = default);
}
