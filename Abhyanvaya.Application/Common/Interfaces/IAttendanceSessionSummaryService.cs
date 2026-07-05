namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Synchronizes pre-calculated recognition summary counters on <see cref="Domain.Entities.AttendanceSession"/>.
/// </summary>
/// <remarks>
/// This service updates tracked session fields only. Callers must persist changes through
/// <see cref="Internal.ConcurrencyExceptionHelper.SaveChangesAsync"/> so optimistic concurrency conflicts
/// on <c>RowVersion</c> are translated consistently.
/// </remarks>
public interface IAttendanceSessionSummaryService
{
    /// <summary>
    /// Recalculates and applies recognition summary fields on the session from current recognition rows.
    /// Does not persist changes; callers must invoke <see cref="IApplicationDbContext.SaveChangesAsync"/>.
    /// </summary>
    Task SyncSessionSummaryAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default);
}
