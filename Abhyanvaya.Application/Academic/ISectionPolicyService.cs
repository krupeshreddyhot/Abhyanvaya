using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

public interface ISectionPolicyService
{
    Task<IReadOnlyList<SectionPolicyDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<SectionPolicyDto> UpsertAsync(UpsertSectionPolicyRequest request, CancellationToken cancellationToken = default);
    Task<ResolvedSectionPolicyDto> ResolveForSectionAsync(int sectionId, CancellationToken cancellationToken = default);
}
