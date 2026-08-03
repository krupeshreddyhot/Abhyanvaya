using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public interface IHolidayTypeCatalogService
{
    Task<IReadOnlyList<HolidayTypeCatalogDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<HolidayTypeCatalogDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<HolidayTypeCatalogDto> CreateAsync(CreateHolidayTypeCatalogRequest request, CancellationToken cancellationToken = default);
    Task<HolidayTypeCatalogDto> UpdateAsync(UpdateHolidayTypeCatalogRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
}
