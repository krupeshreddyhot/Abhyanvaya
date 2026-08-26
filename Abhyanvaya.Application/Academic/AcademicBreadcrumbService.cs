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

    public Task<AcademicOperationalBreadcrumbOutcome> BuildOperationalContextBreadcrumbAsync(
        AcademicOperationalContext context,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.Breadcrumb,
            "AcademicBreadcrumb.OperationalContext",
            async ct =>
            {
                ArgumentNullException.ThrowIfNull(context);
                var model = await _tree.BuildTreeAsync(includeInactive: true, cancellationToken: ct);

                var validation = AcademicOperationalContextValidator.Validate(_tree, model, context);
                if (!validation.IsValid)
                {
                    _logger.LogWarning(
                        "Academic operational breadcrumb rejected invalid context Error={Error} ProgramId={ProgramId} CourseId={CourseId} GroupId={GroupId} SemesterId={SemesterId} SectionId={SectionId} SubjectId={SubjectId}",
                        validation.Error,
                        context.ProgramId,
                        context.CourseId,
                        context.GroupId,
                        context.SemesterId,
                        context.SectionId,
                        context.SubjectId);
                    return AcademicOperationalBreadcrumbOutcome.Invalid(validation.Error ?? "Invalid academic operational context.");
                }

                var crumb = AcademicOperationalBreadcrumbComposer.Compose(_tree, model, context);
                _logger.LogInformation(
                    "Academic operational breadcrumb built Segments={Segments} Path={Path}",
                    crumb.Items.Count, crumb.DisplayPath);
                return AcademicOperationalBreadcrumbOutcome.Valid(crumb);
            },
            cancellationToken);

    private static AcademicBreadcrumb FromPath(IReadOnlyList<AcademicHierarchyNode> path)
        => new(path.Select(n => new AcademicBreadcrumbItem(n.NodeId, n.EntityType, n.EntityId, n.DisplayName, n.Code)).ToList());
}
