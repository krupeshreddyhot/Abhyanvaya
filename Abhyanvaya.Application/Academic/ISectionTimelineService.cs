using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B.5 — Read-only audit projection from versions + lifecycle transitions.</summary>
public interface ISectionTimelineService
{
    Task<IReadOnlyList<SectionTimelineEventDto>> GetTimelineAsync(int sectionId, CancellationToken cancellationToken = default);
}
