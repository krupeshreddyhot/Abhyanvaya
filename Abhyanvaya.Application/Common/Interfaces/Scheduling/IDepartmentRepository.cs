using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

/// <summary>
/// Read-only Catalog Department access for Scheduling consumers (SSOT: Catalog owns Department).
/// </summary>
public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> ListAsync(int tenantId, int? collegeId, bool? isActive, CancellationToken cancellationToken = default);

    Task<Department?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<bool> IsReferencedBySchedulingAsync(int tenantId, int departmentId, CancellationToken cancellationToken = default);
}
