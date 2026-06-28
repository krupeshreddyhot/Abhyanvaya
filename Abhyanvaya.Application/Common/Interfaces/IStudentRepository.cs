using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Student?> GetByIdForTenantAsync(int id, int tenantId, CancellationToken cancellationToken = default);
}
