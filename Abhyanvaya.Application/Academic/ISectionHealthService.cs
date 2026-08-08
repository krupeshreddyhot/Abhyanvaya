using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B.5 — Read-only section health (Healthy | Warning | Critical).</summary>
public interface ISectionHealthService
{
    Task<SectionHealthReportDto> EvaluateAsync(int sectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionHealthReportDto>> EvaluateManyAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);
}
