namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Wake signal for enrollment background workers (docs/AI20_ENROLLMENT_BACKGROUND.md §3.1).
/// The durable queue is <see cref="Domain.Entities.StudentEnrollmentItem"/> rows — this is an optimization only.
/// </summary>
public interface IEnrollmentJobQueue
{
    void SignalWork();

    IAsyncEnumerable<Guid> DequeueClaimedJobIdsAsync(CancellationToken cancellationToken);
}
