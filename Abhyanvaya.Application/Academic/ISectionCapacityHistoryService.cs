using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.Academic;

public interface ISectionCapacityHistoryService
{
    Task RecordAsync(Section section, int currentStrength, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectionCapacityHistoryDto>> GetCapacityHistoryAsync(int sectionId, CancellationToken cancellationToken = default);
}
