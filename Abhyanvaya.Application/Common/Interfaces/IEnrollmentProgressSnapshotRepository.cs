namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentProgressSnapshotRepository
{
    Task AppendAsync(
        Domain.Entities.StudentEnrollmentProgressSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
