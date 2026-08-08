using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B.5 — Read-only split preview. Never writes or transitions lifecycle.</summary>
public interface ISplitPreviewService
{
    Task<SplitPreviewEngineDto> PreviewAsync(
        int sourceSectionId,
        int childCount = 2,
        CancellationToken cancellationToken = default);
}
