using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Common.Interfaces.Scheduling;

public interface IConflictDetectionRepository
{
    Task<ConflictDetectionRun?> GetLatestRunAsync(int tenantId, int? timetableId, int? academicYearId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConflictDetectionRun>> ListRecentRunsAsync(int tenantId, int take, CancellationToken cancellationToken = default);
    Task<ConflictDetectionRun> SaveRunAsync(ConflictDetectionRun run, IReadOnlyList<ConflictFinding> findings, CancellationToken cancellationToken = default);
}
