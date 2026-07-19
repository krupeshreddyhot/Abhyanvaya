using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Enrollment.Persistence;

public sealed class NoOpEnrollmentPersistenceMetrics : IEnrollmentPersistenceMetrics
{
    public void RecordSuccess(
        int embeddingDimension,
        long writeDurationMs,
        long databaseDurationMs,
        int rowsInserted,
        int rowsUpdated,
        bool isDuplicate)
    {
    }

    public void RecordFailure(string failureCode)
    {
    }
}
