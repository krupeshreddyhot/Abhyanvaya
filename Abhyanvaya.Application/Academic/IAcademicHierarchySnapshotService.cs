using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1A.6 — Feature-flagged daily hierarchy snapshots (disabled by default).</summary>
public interface IAcademicHierarchySnapshotService
{
    bool IsEnabled { get; }
    Task<AcademicHierarchySnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<AcademicHierarchySnapshot?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<AcademicHierarchySnapshot?> GenerateTodayAsync(CancellationToken cancellationToken = default);
}
