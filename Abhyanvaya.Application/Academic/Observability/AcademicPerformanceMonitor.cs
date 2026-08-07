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
}
