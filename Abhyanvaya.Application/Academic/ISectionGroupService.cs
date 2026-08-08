using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

public interface ISectionGroupService
{
    Task<IReadOnlyList<SectionGroupDto>> ListAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default);

    Task<SectionGroupDto?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<SectionGroupDto> CreateAsync(CreateSectionGroupRequest request, CancellationToken cancellationToken = default);
    Task<SectionGroupDto> UpdateMembersAsync(int id, UpdateSectionGroupMembersRequest request, CancellationToken cancellationToken = default);
}
