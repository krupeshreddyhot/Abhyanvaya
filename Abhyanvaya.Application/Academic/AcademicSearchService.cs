using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Academic.ReadModels;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

public sealed class AcademicSearchService : IAcademicSearchService
{
    private readonly IAcademicTreeService _tree;
    private readonly IAcademicBreadcrumbService _breadcrumbs;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly ILogger<AcademicSearchService> _logger;

    public AcademicSearchService(
        IAcademicTreeService tree,
        IAcademicBreadcrumbService breadcrumbs,
        IAcademicTelemetryService telemetry,
        ILogger<AcademicSearchService> logger)
    {
        _tree = tree;
        _breadcrumbs = breadcrumbs;
        _telemetry = telemetry;
        _logger = logger;
    }

    public Task<AcademicSearchResult?> FindNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.Search,
            "AcademicSearch.Execute",
            async ct =>
            {
                var model = await _tree.BuildTreeAsync(includeInactive: true, cancellationToken: ct);
                var node = _tree.FindByNodeId(model, nodeId);
                if (node is null) return null;
                var path = await _breadcrumbs.BuildBreadcrumbAsync(nodeId, ct);
                _logger.LogInformation("Academic search node NodeId={NodeId} Found={Found}", nodeId, true);
                return new AcademicSearchResult(node with { Children = [] }, path);
            },
            cancellationToken);

    public Task<IReadOnlyList<AcademicSearchResult>> FindCourseAsync(string query, CancellationToken cancellationToken = default)
        => FindByTypeAsync("Course", query, cancellationToken);

    public Task<IReadOnlyList<AcademicSearchResult>> FindSemesterAsync(string query, CancellationToken cancellationToken = default)
        => FindByTypeAsync("Semester", query, cancellationToken);

    public Task<IReadOnlyList<AcademicSearchResult>> FindSectionAsync(string query, CancellationToken cancellationToken = default)
        => FindByTypeAsync("Section", query, cancellationToken);

    public Task<IReadOnlyList<AcademicSearchResult>> FindSubjectAsync(string query, CancellationToken cancellationToken = default)
        => FindByTypeAsync("Subject", query, cancellationToken);

    private Task<IReadOnlyList<AcademicSearchResult>> FindByTypeAsync(
        string entityType,
        string query,
        CancellationToken cancellationToken)
        => _telemetry.TrackAsync(
            AcademicOperations.Search,
            "AcademicSearch.Execute",
            async ct =>
            {
                var model = await _tree.BuildTreeAsync(includeInactive: true, cancellationToken: ct);
                var flat = _tree.FlattenTree(model);
                var q = (query ?? "").Trim();
                var matches = flat
                    .Where(n => string.Equals(n.EntityType, entityType, StringComparison.OrdinalIgnoreCase))
                    .Where(n =>
                        string.IsNullOrEmpty(q)
                        || n.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || n.Code.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Take(50)
                    .ToList();

                var results = new List<AcademicSearchResult>(matches.Count);
                foreach (var match in matches)
                {
                    var pathNodes = _tree.GetPath(model, match.NodeId);
                    var breadcrumb = new AcademicBreadcrumb(
                        pathNodes.Select(n => new AcademicBreadcrumbItem(n.NodeId, n.EntityType, n.EntityId, n.DisplayName, n.Code)).ToList());
                    results.Add(new AcademicSearchResult(match, breadcrumb));
                }

                _logger.LogInformation(
                    "Academic search EntityType={EntityType} Query={Query} Matches={Matches}",
                    entityType, q, results.Count);
                return (IReadOnlyList<AcademicSearchResult>)results;
            },
            cancellationToken);
}
