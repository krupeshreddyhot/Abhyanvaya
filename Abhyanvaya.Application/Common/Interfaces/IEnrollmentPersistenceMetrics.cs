namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentPersistenceMetrics
{
    void RecordSuccess(
        int embeddingDimension,
        long writeDurationMs,
        long databaseDurationMs,
        int rowsInserted,
        int rowsUpdated,
        bool isDuplicate);

    void RecordFailure(string failureCode);
}
