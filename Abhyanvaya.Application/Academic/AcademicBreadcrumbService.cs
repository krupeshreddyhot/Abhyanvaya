using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Academic.ReadModels;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

public sealed class AcademicBreadcrumbService : IAcademicBreadcrumbService
{
    private readonly IAcademicTreeService _tree;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly ILogger<AcademicBreadcrumbService> _logger;

    public AcademicBreadcrumbService(
        IAcademicTreeService tree,
        IAcademicTelemetryService telemetry,
        ILogger<AcademicBreadcrumbService> logger)
    {
        _tree = tree;
        _telemetry = telemetry;
        _logger = logger;
    }

    public Task<AcademicBreadcrumb> BuildBreadcrumbAsync(string nodeId, CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.Breadcrumb,
            "AcademicBreadcrumb.Build",
            async ct =>
            {
                var model = await _tree.BuildTreeAsync(includeInactive: true, cancellationToken: ct);
                var crumb = FromPath(_tree.GetPath(model, nodeId));
                _logger.LogInformation(
                    "Academic breadcrumb built NodeId={NodeId} Segments={Segments} Path={Path}",
                    nodeId, crumb.Items.Count, crumb.DisplayPath);
                return crumb;
            },
            cancellationToken);

    public Task<AcademicBreadcrumb> BuildProgramBreadcrumbAsync(int programId, CancellationToken cancellationToken = default)
        => BuildBreadcrumbAsync($"Program:{programId}", cancellationToken);

    public Task<AcademicBreadcrumb> BuildCourseBreadcrumbAsync(int courseId, CancellationToken cancellationToken = default)
        => BuildBreadcrumbAsync($"Course:{courseId}", cancellationToken);

    public Task<AcademicBreadcrumb> BuildSectionBreadcrumbAsync(int sectionId, CancellationToken cancellationToken = default)
        => BuildBreadcrumbAsync($"Section:{sectionId}", cancellationToken);

    private static AcademicBreadcrumb FromPath(IReadOnlyList<AcademicHierarchyNode> path)
        => new(path.Select(n => new AcademicBreadcrumbItem(n.NodeId, n.EntityType, n.EntityId, n.DisplayName, n.Code)).ToList());
}
