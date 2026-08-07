using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1A.6 — Statistics cache separated from hierarchy cache.
/// Uses existing <see cref="Common.Interfaces.ICacheService"/>; never shares hierarchy keys.
/// </summary>
public interface IAcademicStatisticsCache
{
    Task WarmAsync(CancellationToken cancellationToken = default);
    Task InvalidateAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProgramStatisticsDto>?> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task SetStatisticsAsync(IReadOnlyList<ProgramStatisticsDto> statistics, CancellationToken cancellationToken = default);
    Task<AcademicHierarchyStatisticsDto?> GetHierarchyStatisticsAsync(CancellationToken cancellationToken = default);
    Task SetHierarchyStatisticsAsync(AcademicHierarchyStatisticsDto statistics, CancellationToken cancellationToken = default);
}
