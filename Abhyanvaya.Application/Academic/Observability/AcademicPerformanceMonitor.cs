namespace Abhyanvaya.Application.Academic.Observability;

public sealed class AcademicPerformanceMonitor : IAcademicPerformanceMonitor
{
    private readonly AcademicMetricsStore _store;

    public AcademicPerformanceMonitor(AcademicMetricsStore store) => _store = store;

    public AcademicOperationMetricsDto GetOperation(string operation) =>
        _store.GetOperationMetrics(operation, BudgetFor(operation));

    public AcademicPerformanceReportDto GetReport()
    {
        var ops = new[]
        {
            AcademicOperations.HierarchyBuild,
            AcademicOperations.TreeBuild,
            AcademicOperations.Search,
            AcademicOperations.Breadcrumb,
            AcademicOperations.StructureApi,
            AcademicOperations.Catalog,
            AcademicOperations.HierarchyService,
            AcademicOperations.Snapshot,
            AcademicOperations.ArchitectureGuard,
            AcademicOperations.ProgramStatistics,
            AcademicOperations.SectionMergePreview,
            AcademicOperations.SectionSplitPreview,
            AcademicOperations.SectionPolicyResolve,
            AcademicOperations.SectionCapacityRecommend,
            AcademicOperations.SectionHealth,
            AcademicOperations.SectionTimeline,
            AcademicOperations.AllocationContextBuild,
            AcademicOperations.AllocationContextRefresh,
            AcademicOperations.AllocationSnapshot,
            AcademicOperations.AllocationValidation,
            AcademicOperations.AllocationReadiness,
            AcademicOperations.AllocationHealth,
            AcademicOperations.AllocationEngineRun,
            AcademicOperations.AllocationSimulation,
            AcademicOperations.AllocationComparison,
            AcademicOperations.AllocationApproval,
            AcademicOperations.AllocationScoring,
            AcademicOperations.AllocationConstraintEval,
            AcademicOperations.AllocationReplay,
            AcademicOperations.AllocationScenarioCreate,
            AcademicOperations.AllocationGovernance,
        }.Select(GetOperation).ToList();

        return new AcademicPerformanceReportDto
        {
            GeneratedUtc = DateTime.UtcNow,
            Operations = ops,
            AllWithinBudget = ops.All(o => o.WithinBudget),
        };
    }

    private static double BudgetFor(string operation) => operation switch
    {
        AcademicOperations.HierarchyBuild => AcademicPerformanceBudgets.HierarchyCacheMs,
        AcademicOperations.TreeBuild => AcademicPerformanceBudgets.TreeBuildMs,
        AcademicOperations.Search => AcademicPerformanceBudgets.SearchMs,
        AcademicOperations.Breadcrumb => AcademicPerformanceBudgets.BreadcrumbMs,
        AcademicOperations.StructureApi => AcademicPerformanceBudgets.AcademicStructureApiMs,
        AcademicOperations.ProgramStatistics => AcademicPerformanceBudgets.StatisticsCacheMs,
        _ => AcademicPerformanceBudgets.AcademicStructureApiMs,
    };
}

public static class AcademicOperations
{
    public const string HierarchyBuild = "hierarchy.build";
    public const string TreeBuild = "tree.build";
    public const string Search = "search.execute";
    public const string Breadcrumb = "breadcrumb.build";
    public const string StructureApi = "structure.api";
    public const string Catalog = "catalog.service";
    public const string HierarchyService = "hierarchy.service";
    public const string Snapshot = "snapshot.generate";
    public const string ArchitectureGuard = "architecture.guard";
    public const string ProgramStatistics = "program.statistics";

    // AI29.1B.5 — Section operations hardening
    public const string SectionMergePreview = "section.merge.preview";
    public const string SectionSplitPreview = "section.split.preview";
    public const string SectionPolicyResolve = "section.policy.resolve";
    public const string SectionCapacityRecommend = "section.capacity.recommend";
    public const string SectionHealth = "section.health.evaluate";
    public const string SectionTimeline = "section.timeline.build";

    // AI29.1B.7 — Allocation platform
    public const string AllocationContextBuild = "allocation.context.build";
    public const string AllocationContextRefresh = "allocation.context.refresh";
    public const string AllocationSnapshot = "allocation.snapshot.generate";
    public const string AllocationValidation = "allocation.validation";
    public const string AllocationReadiness = "allocation.readiness";
    public const string AllocationHealth = "allocation.health";
    public const string AllocationCacheHit = "allocation.cache.hit";
    public const string AllocationCacheMiss = "allocation.cache.miss";
    public const string AllocationCacheWarm = "allocation.cache.warm";
    public const string AllocationCacheRefresh = "allocation.cache.refresh";

    // AI29.1C — Allocation engine
    public const string AllocationEngineRun = "allocation.engine.run";
    public const string AllocationSimulation = "allocation.simulation";
    public const string AllocationComparison = "allocation.comparison";
    public const string AllocationApproval = "allocation.approval";
    public const string AllocationScoring = "allocation.scoring";
    public const string AllocationConstraintEval = "allocation.constraint.eval";
    public const string AllocationReplay = "allocation.replay";
    public const string AllocationScenarioCreate = "allocation.scenario.create";
    public const string AllocationGovernance = "allocation.governance";
}
