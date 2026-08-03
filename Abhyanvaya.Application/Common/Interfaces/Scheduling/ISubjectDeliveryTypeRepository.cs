using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface ISubjectDeliveryTypeRepository
{
    Task<IReadOnlyList<SubjectDeliveryType>> ListAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default);
    Task<SubjectDeliveryType?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<SubjectDeliveryType?> GetByCodeAsync(int tenantId, string code, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task AddAsync(SubjectDeliveryType entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<SubjectDeliveryType> entities, CancellationToken cancellationToken = default);
}
