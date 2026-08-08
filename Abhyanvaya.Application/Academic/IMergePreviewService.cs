using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B.5 — Read-only merge preview. Never writes or transitions lifecycle.</summary>
public interface IMergePreviewService
{
    Task<MergePreviewEngineDto> PreviewAsync(
        IReadOnlyList<int> sourceSectionIds,
        int targetSectionId,
        CancellationToken cancellationToken = default);
}
