using Abhyanvaya.Domain.Entities.Scheduling;



namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;



public interface ISubjectCategoryRepository

{

    Task<IReadOnlyList<SubjectCategory>> ListAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default);

    Task<SubjectCategory?> GetByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    Task<SubjectCategory?> GetByCodeAsync(int tenantId, string code, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(SubjectCategory entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<SubjectCategory> entities, CancellationToken cancellationToken = default);

}

