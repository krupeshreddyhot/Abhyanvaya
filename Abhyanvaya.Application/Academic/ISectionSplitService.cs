using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

public interface ISectionSplitService
{
    Task<SectionSplitPreviewDto> ValidateAsync(SectionSplitValidateRequest request, CancellationToken cancellationToken = default);
    Task<SectionSplitPreviewDto> PreviewAsync(SectionSplitValidateRequest request, CancellationToken cancellationToken = default);
    Task<SectionSplitTransactionDto> CommitAsync(SectionSplitCommitRequest request, CancellationToken cancellationToken = default);
    Task<SectionSplitTransactionDto> ReverseAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionSplitTransactionDto>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionLineageDto>> GetLineageAsync(int sectionId, CancellationToken cancellationToken = default);
}
