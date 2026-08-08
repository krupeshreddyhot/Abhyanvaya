using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B — Sole owner of section lifecycle transitions (state machine).</summary>
public interface ISectionLifecycleService
{
    IReadOnlyList<string> GetAllStates();
    IReadOnlyList<string> GetAllowedTransitions(string currentStatus);
    IReadOnlyList<SectionTypeOptionDto> GetSectionTypes();

    Task<SectionDto> TransitionAsync(int sectionId, SectionLifecycleTransitionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionLifecycleHistoryDto>> GetHistoryAsync(int sectionId, CancellationToken cancellationToken = default);

    /// <summary>Internal helper for merge/split/create — still validates via state machine.</summary>
    Task ApplyStatusAsync(int sectionId, string targetStatus, string? reason, CancellationToken cancellationToken = default);
}
