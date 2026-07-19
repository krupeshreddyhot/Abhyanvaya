using Abhyanvaya.Application.EnrollmentApi;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentDashboardService
{
    Task<EnrollmentDashboardResponse> GetDashboardAsync(int tenantId, int? collegeId, CancellationToken cancellationToken = default);
}

public interface IEnrollmentReadinessService
{
    Task<EnrollmentReadinessResult> EvaluateAsync(
        int tenantId,
        int collegeId,
        int academicYear,
        EnrollmentPreviewRequest? preview = null,
        CancellationToken cancellationToken = default);
}

public interface IEnrollmentHistoryService
{
    Task<PagedResult<BatchSummary>> GetHistoryAsync(int tenantId, EnrollmentFilters filters, CancellationToken cancellationToken = default);
    Task<PagedResult<BatchSummary>> GetBatchesAsync(int tenantId, EnrollmentFilters filters, CancellationToken cancellationToken = default);
    Task<BatchDetailDto?> GetBatchDetailAsync(Guid batchId, int tenantId, CancellationToken cancellationToken = default);
    Task<BatchProgressDto?> GetBatchProgressAsync(Guid batchId, int tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<StudentEnrollmentExplorerItem>> GetBatchStudentsAsync(
        Guid batchId,
        int tenantId,
        EnrollmentFilters filters,
        CancellationToken cancellationToken = default);
    Task<EnrollmentPreview> PreviewAsync(EnrollmentPreviewRequest request, CancellationToken cancellationToken = default);
    Task<CreateBatchResponse> CreateBatchAsync(CreateEnrollmentBatchApiRequest request, int tenantId, int userId, CancellationToken cancellationToken = default);
}

public interface IBatchCancellationService
{
    Task<BatchCommandResponse> CancelAsync(Guid batchId, int tenantId, int userId, CancellationToken cancellationToken = default);
}

public interface IBatchRetryService
{
    Task<BatchCommandResponse> RetryAsync(Guid batchId, int tenantId, int userId, CancellationToken cancellationToken = default);
}

public interface IEnrollmentEventPublisher
{
    Task PublishBatchCreatedAsync(Guid batchId, int totalStudents, CancellationToken cancellationToken = default);
    Task PublishBatchStartedAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task PublishBatchProgressAsync(BatchProgressDto progress, CancellationToken cancellationToken = default);
    Task PublishBatchCompletedAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task PublishBatchFailedAsync(Guid batchId, string reason, CancellationToken cancellationToken = default);
    Task PublishBatchCancelledAsync(Guid batchId, CancellationToken cancellationToken = default);
}
