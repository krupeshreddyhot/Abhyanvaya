using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI29.1B — Sole component responsible for occupancy / capacity calculations.
/// Controllers, UI, reports, and dashboards must consume these results (no duplicated formulas).
/// </summary>
public interface ISectionCapacityEngine
{
    SectionCapacitySnapshotDto Calculate(Section section, int currentStrength, TenantSectionCapacityPolicy? policy);

    Task<SectionCapacitySnapshotDto> GetOccupancyAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionCapacitySnapshotDto>> GetOccupancyAsync(
        IEnumerable<int>? sectionIds = null,
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);

    Task<SectionCapacitySummaryDto> GetCapacitySummaryAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SectionCapacitySnapshotDto>> GetOverCapacityAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionCapacitySnapshotDto>> GetUnderCapacityAsync(CancellationToken cancellationToken = default);

    Task UpdateCapacityAsync(int sectionId, UpdateSectionCapacityRequest request, CancellationToken cancellationToken = default);
    Task<TenantSectionCapacityPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<TenantSectionCapacityPolicyDto> UpsertPolicyAsync(UpsertTenantSectionCapacityPolicyRequest request, CancellationToken cancellationToken = default);

    Task<SectionCapacityAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);

    /// <summary>Hard-limit check used by assignment flows (warnings vs block depends on policy).</summary>
    Task EnsureCanAcceptStudentAsync(Section section, CancellationToken cancellationToken = default);
}
