using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

public interface ISectionMergeService
{
    Task<SectionMergePreviewDto> ValidateAsync(SectionMergeValidateRequest request, CancellationToken cancellationToken = default);
    Task<SectionMergePreviewDto> PreviewAsync(SectionMergeValidateRequest request, CancellationToken cancellationToken = default);
    Task<SectionMergeTransactionDto> CommitAsync(SectionMergeCommitRequest request, CancellationToken cancellationToken = default);
    Task<SectionMergeTransactionDto> ReverseAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionMergeTransactionDto>> GetHistoryAsync(CancellationToken cancellationToken = default);
}
