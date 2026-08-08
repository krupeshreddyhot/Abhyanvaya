using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B.5 — Append-only immutable section versions.</summary>
public interface ISectionVersioningService
{
    Task<SectionVersionDto> RecordAsync(
        Section section,
        string operation,
        string? reason,
        int currentStrength,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SectionVersionDto>> GetVersionsAsync(int sectionId, CancellationToken cancellationToken = default);
}
