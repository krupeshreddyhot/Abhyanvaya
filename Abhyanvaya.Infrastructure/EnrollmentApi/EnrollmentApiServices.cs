using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Application.EnrollmentApi;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.ArtifactStorage;
using Abhyanvaya.Infrastructure.Enrollment.PhotoProviders;
using Abhyanvaya.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.EnrollmentApi;

public sealed class EnrollmentDashboardService : IEnrollmentDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly IAIHealthService _healthService;
    private readonly IArtifactUploadQueue _artifactQueue;
    private readonly IConfiguration _configuration;
    private readonly ExamBranchPhotoProviderOptions _photoOptions;
    private readonly ArtifactStorageOptions _artifactStorageOptions;
    private readonly IHostEnvironment _environment;

    public EnrollmentDashboardService(
        IApplicationDbContext context,
        IAIHealthService healthService,
        IArtifactUploadQueue artifactQueue,
        IConfiguration configuration,
        IOptions<ExamBranchPhotoProviderOptions> photoOptions,
        IOptions<ArtifactStorageOptions> artifactStorageOptions,
        IHostEnvironment environment)
    {
        _context = context;
        _healthService = healthService;
        _artifactQueue = artifactQueue;
        _configuration = configuration;
        _photoOptions = photoOptions.Value;
        _artifactStorageOptions = artifactStorageOptions.Value;
        _environment = environment;
    }

    public async Task<EnrollmentDashboardResponse> GetDashboardAsync(int tenantId, int? collegeId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var studentQuery = _context.Students.AsNoTracking();
        if (tenantId > 0)
        {
            studentQuery = studentQuery.Where(s => s.TenantId == tenantId);
        }

        var totalStudents = await studentQuery.CountAsync(cancellationToken);
        var embedded = await _context.StudentFaceEmbeddings.AsNoTracking()
            .Where(e => tenantId <= 0 || e.TenantId == tenantId)
            .CountAsync(cancellationToken);

        var batchQuery = _context.StudentEnrollmentBatches.AsNoTracking();
        if (tenantId > 0)
        {
            batchQuery = batchQuery.Where(b => b.TenantId == tenantId);
        }

        if (collegeId is > 0)
        {
            batchQuery = batchQuery.Where(b => b.CollegeId == collegeId);
        }

        var batches = await batchQuery.ToListAsync(cancellationToken);

        var runningBatch = batches
            .Where(b => b.Status == BatchStatus.Created || b.Status == BatchStatus.Running)
            .OrderByDescending(b => b.CreatedUtc)
            .FirstOrDefault();

        var pending = batches.Sum(b =>
            b.PendingCount + b.DownloadingCount + b.ValidatingCount + b.EmbeddingCount + b.RetryRequiredCount);
        var failed = batches.Sum(b => b.FailedCount);
        var completed = batches.Sum(b => b.CompletedCount);

        var processedToday = batches
            .Where(b => b.CompletedUtc >= today)
            .Sum(b => b.CompletedCount);

        var successRate = completed + failed == 0 ? 0m : (decimal)completed / (completed + failed);

        var health = await _healthService.GetPlatformHealthAsync(cancellationToken);
        var systemStatus = MapSystemStatus(
            health,
            _photoOptions.BaseUrlTemplate,
            ArtifactStorageProviderSelection.ResolveProviderName(_artifactStorageOptions, _environment));

        return new EnrollmentDashboardResponse
        {
            Dashboard = new EnrollmentDashboardDto
            {
                TotalStudents = totalStudents,
                EligibleStudents = Math.Max(0, totalStudents - embedded),
                Embedded = embedded,
                Pending = pending,
                Failed = failed,
                ProcessedToday = processedToday,
                RunningBatchId = runningBatch?.Id,
                QueueLength = _artifactQueue.QueueDepth,
                AverageDuration = TimeSpan.Zero,
                SuccessRate = successRate,
            },
            SystemStatus = systemStatus,
            Configuration = BuildConfiguration(),
        };
    }

    private EnrollmentConfigurationDto BuildConfiguration() =>
        new()
        {
            PhotoProvider = "ExamBranch",
            EmbeddingEngine = "InsightFace",
            RecognitionEngine = "InsightFace",
            StorageProvider = ArtifactStorageProviderSelection.ResolveProviderName(_artifactStorageOptions, _environment),
            RetryPolicy = "3 Attempts",
            DownloadThreads = 4,
            ImageFormat = "JPEG",
            EmbeddingDimensions = 512,
            PhotoUrlTemplate = _photoOptions.BaseUrlTemplate,
        };

    private static EnrollmentSystemStatusDto MapSystemStatus(
        Application.AIOperations.AIPlatformHealthReport health,
        string photoUrlTemplate,
        string storageProviderName)
    {
        string StatusFor(string component) =>
            health.Checks.FirstOrDefault(c => c.ComponentName == component)?.Status.ToString() ?? "Unknown";

        return new EnrollmentSystemStatusDto
        {
            PhotoProvider = "ExamBranch",
            PhotoProviderStatus = string.IsNullOrWhiteSpace(photoUrlTemplate) ? "Offline" : "Ready",
            EmbeddingEngine = "InsightFace",
            EmbeddingEngineStatus = StatusFor(AIOperationsComponents.EmbeddingEngine),
            RecognitionEngine = "InsightFace",
            RecognitionEngineStatus = StatusFor(AIOperationsComponents.Recognition),
            StorageProvider = ArtifactStorageProviderSelection.ResolveDisplayName(storageProviderName),
            StorageStatus = StatusFor(AIOperationsComponents.Storage),
            WorkerStatus = StatusFor(AIOperationsComponents.Workers),
        };
    }
}

public sealed class EnrollmentReadinessService : IEnrollmentReadinessService
{
    private readonly IAIHealthService _healthService;
    private readonly IStudentEnrollmentBatchRepository _batchRepository;
    private readonly IEnrollmentEligibleStudentQuery _eligibleQuery;
    private readonly IConfiguration _configuration;

    public EnrollmentReadinessService(
        IAIHealthService healthService,
        IStudentEnrollmentBatchRepository batchRepository,
        IEnrollmentEligibleStudentQuery eligibleQuery,
        IConfiguration configuration)
    {
        _healthService = healthService;
        _batchRepository = batchRepository;
        _eligibleQuery = eligibleQuery;
        _configuration = configuration;
    }

    public async Task<EnrollmentReadinessResult> EvaluateAsync(
        int tenantId,
        int collegeId,
        int academicYear,
        EnrollmentPreviewRequest? preview = null,
        CancellationToken cancellationToken = default)
    {
        var reasons = new List<string>();
        var health = await _healthService.GetPlatformHealthAsync(cancellationToken);

        var photoReady = !string.IsNullOrWhiteSpace(_configuration["StudentPhotoProvider:ExamBranch:BaseUrlTemplate"]);
        var storageReady = IsHealthy(health, AIOperationsComponents.Storage);
        var recognitionReady = IsHealthy(health, AIOperationsComponents.Recognition);
        var workerReady = IsHealthy(health, AIOperationsComponents.Workers);
        var configValid = !string.IsNullOrWhiteSpace(_configuration["Jwt:Key"]);

        if (!photoReady) reasons.Add("ExamBranch photo URL template is not configured.");
        if (!storageReady)
        {
            var storageMessage = health.Checks
                .FirstOrDefault(c => c.ComponentName == AIOperationsComponents.Storage)
                ?.Message;
            reasons.Add(storageMessage ?? "Artifact storage is not ready.");
        }
        if (!recognitionReady) reasons.Add("Recognition engine is not ready.");
        if (!workerReady) reasons.Add("Background workers are not ready.");
        if (!configValid) reasons.Add("Configuration validation failed.");

        var hasActive = await _batchRepository.HasActiveBatchAsync(tenantId, collegeId, academicYear, cancellationToken);
        Guid? runningBatchId = null;
        if (hasActive)
        {
            var batches = await _batchRepository.GetByCollegeAsync(tenantId, collegeId, academicYear, cancellationToken);
            runningBatchId = batches.FirstOrDefault(b => b.Status is BatchStatus.Created or BatchStatus.Running)?.Id;
            reasons.Add("An enrollment batch is already running for this college and academic year.");
        }

        var criteria = preview ?? new EnrollmentPreviewRequest
        {
            TenantId = tenantId,
            CollegeId = collegeId,
            AcademicYear = academicYear,
        };

        var eligible = await _eligibleQuery.GetEligibleStudentsAsync(new EnrollmentStudentDiscoveryCriteria
        {
            TenantId = tenantId,
            CourseId = criteria.CourseId,
            GroupId = criteria.GroupId,
            Batch = criteria.Batch,
            SubjectId = criteria.SubjectId,
            ForceReEnrollment = criteria.ForceReEnrollment,
        }, cancellationToken);

        if (eligible.Count == 0)
        {
            reasons.Add("No eligible students match the selected filters.");
        }

        var canStart = reasons.Count == 0;

        return new EnrollmentReadinessResult
        {
            CanStart = canStart,
            EligibleStudents = eligible.Count,
            RunningBatchId = runningBatchId,
            PhotoProviderReady = photoReady,
            StorageReady = storageReady,
            RecognitionReady = recognitionReady,
            WorkerReady = workerReady,
            ConfigurationValid = configValid,
            Reasons = reasons,
        };
    }

    private static bool IsHealthy(Application.AIOperations.AIPlatformHealthReport health, string component)
    {
        var check = health.Checks.FirstOrDefault(c => c.ComponentName == component);
        return check?.Status is AIHealthStatus.Ready or AIHealthStatus.Live;
    }
}

public sealed class EnrollmentHistoryService : IEnrollmentHistoryService
{
    private readonly IApplicationDbContext _context;
    private readonly IEnrollmentBatchService _batchService;
    private readonly IEnrollmentEligibleStudentQuery _eligibleQuery;
    private readonly IEnrollmentProgressReporter _progressReporter;
    private readonly IEnrollmentSignalRPublisher _eventPublisher;
    private readonly IAuditService _auditService;

    public EnrollmentHistoryService(
        IApplicationDbContext context,
        IEnrollmentBatchService batchService,
        IEnrollmentEligibleStudentQuery eligibleQuery,
        IEnrollmentProgressReporter progressReporter,
        IEnrollmentSignalRPublisher eventPublisher,
        IAuditService auditService)
    {
        _context = context;
        _batchService = batchService;
        _eligibleQuery = eligibleQuery;
        _progressReporter = progressReporter;
        _eventPublisher = eventPublisher;
        _auditService = auditService;
    }

    public Task<PagedResult<BatchSummary>> GetHistoryAsync(int tenantId, EnrollmentFilters filters, CancellationToken cancellationToken = default) =>
        GetBatchesAsync(tenantId, filters, cancellationToken);

    public async Task<PagedResult<BatchSummary>> GetBatchesAsync(int tenantId, EnrollmentFilters filters, CancellationToken cancellationToken = default)
    {
        var query = _context.StudentEnrollmentBatches.AsNoTracking();
        if (tenantId > 0)
        {
            query = query.Where(b => b.TenantId == tenantId);
        }

        if (filters.CollegeId is > 0)
        {
            query = query.Where(b => b.CollegeId == filters.CollegeId);
        }

        if (filters.AcademicYear is > 0)
        {
            query = query.Where(b => b.AcademicYear == filters.AcademicYear);
        }

        if (filters.Status is not null)
        {
            query = query.Where(b => b.Status == filters.Status);
        }

        var total = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, filters.Page);
        var pageSize = Math.Clamp(filters.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(b => b.CreatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BatchSummary>
        {
            Items = items.Select(MapSummary).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<BatchDetailDto?> GetBatchDetailAsync(Guid batchId, int tenantId, CancellationToken cancellationToken = default)
    {
        var batch = tenantId > 0
            ? await _context.StudentEnrollmentBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId, cancellationToken)
            : await _context.StudentEnrollmentBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var progressDetail = await _progressReporter.UpdateProgressAsync(batchId, batch.TenantId, cancellationToken);
        var summary = MapSummary(batch);

        return new BatchDetailDto
        {
            BatchId = summary.BatchId,
            Status = summary.Status,
            TotalStudents = summary.TotalStudents,
            CompletedCount = summary.CompletedCount,
            FailedCount = summary.FailedCount,
            PendingCount = summary.PendingCount,
            CollegeId = summary.CollegeId,
            AcademicYear = summary.AcademicYear,
            CreatedUtc = summary.CreatedUtc,
            CompletedUtc = summary.CompletedUtc,
            ProgressPercent = summary.ProgressPercent,
            PhotoProviderName = summary.PhotoProviderName,
            UniversityId = batch.UniversityId,
            CreatedBy = batch.CreatedBy,
            StartedUtc = batch.StartedUtc,
            CorrelationId = batch.CorrelationId,
            PipelineVersion = batch.PipelineVersion,
            EstimatedRemaining = progressDetail?.Metrics.EstimatedCompletionUtc.HasValue == true
                ? progressDetail.Metrics.EstimatedCompletionUtc.Value - DateTime.UtcNow
                : null,
        };
    }

    public async Task<BatchProgressDto?> GetBatchProgressAsync(Guid batchId, int tenantId, CancellationToken cancellationToken = default)
    {
        var batch = tenantId > 0
            ? await _context.StudentEnrollmentBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId && b.TenantId == tenantId, cancellationToken)
            : await _context.StudentEnrollmentBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        var progressDetail = await _progressReporter.UpdateProgressAsync(batchId, batch.TenantId, cancellationToken);
        var percent = progressDetail?.Metrics.CompletionPercentage ?? CalculateBatchProgressPercent(batch);

        return new BatchProgressDto
        {
            BatchId = batchId,
            State = MapProgressState(batch.Status),
            Percentage = percent,
            EstimatedRemaining = progressDetail?.Metrics.EstimatedCompletionUtc.HasValue == true
                ? progressDetail.Metrics.EstimatedCompletionUtc.Value - DateTime.UtcNow
                : null,
            Queued = batch.PendingCount,
            Downloading = batch.DownloadingCount,
            Validating = batch.ValidatingCount,
            Embedding = batch.EmbeddingCount,
            Completed = batch.CompletedCount,
            Failed = batch.FailedCount,
            Cancelled = batch.CancelledCount,
        };
    }

    public async Task<PagedResult<StudentEnrollmentExplorerItem>> GetBatchStudentsAsync(
        Guid batchId,
        int tenantId,
        EnrollmentFilters filters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StudentEnrollmentItems.AsNoTracking().Where(i => i.BatchId == batchId);
        if (tenantId > 0)
        {
            query = query.Where(i => i.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            query = query.Where(i => i.SourceUrl.Contains(filters.Search));
        }

        var total = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, filters.Page);
        var pageSize = Math.Clamp(filters.PageSize, 1, 200);

        var items = await query
            .Join(_context.Students.AsNoTracking(), i => i.StudentId, s => s.Id, (i, s) => new { Item = i, Student = s })
            .OrderBy(x => x.Student.StudentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StudentEnrollmentExplorerItem>
        {
            Items = items.Select(x => MapStudent(x.Item, x.Student.StudentNumber)).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<EnrollmentPreview> PreviewAsync(EnrollmentPreviewRequest request, CancellationToken cancellationToken = default)
    {
        var eligible = await _eligibleQuery.GetEligibleStudentsAsync(new EnrollmentStudentDiscoveryCriteria
        {
            TenantId = request.TenantId,
            CourseId = request.CourseId,
            GroupId = request.GroupId,
            Batch = request.Batch,
            SubjectId = request.SubjectId,
            ForceReEnrollment = request.ForceReEnrollment,
        }, cancellationToken);

        return new EnrollmentPreview
        {
            EligibleStudentCount = eligible.Count,
            SampleStudentNumbers = eligible.Take(10).Select(e => e.StudentNumber).ToList(),
        };
    }

    public async Task<CreateBatchResponse> CreateBatchAsync(
        CreateEnrollmentBatchApiRequest request,
        int tenantId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _batchService.CreateBatchAsync(new EnrollmentBatchRequest
        {
            TenantId = tenantId,
            UniversityId = request.UniversityId,
            CollegeId = request.CollegeId,
            AcademicYear = request.AcademicYear,
            RequestedByUserId = userId,
            CourseId = request.CourseId,
            GroupId = request.GroupId,
            Batch = request.Batch,
            SubjectId = request.SubjectId,
            ForceReEnrollment = request.ForceReEnrollment,
            PhotoProvider = request.PhotoProvider,
        }, cancellationToken);

        if (result.Succeeded && result.BatchId is not null)
        {
            await _auditService.RecordAsync(
                "StudentEnrollmentBatch",
                result.BatchId.Value.ToString(),
                AuditAction.Created,
                newValues: new { request.CollegeId, request.AcademicYear, request.CourseId, request.GroupId, UserId = userId, result.TotalStudents });
            await _eventPublisher.PublishBatchCreatedAsync(tenantId, result.BatchId.Value, result.TotalStudents, cancellationToken);
            await _eventPublisher.PublishBatchStartedAsync(tenantId, result.BatchId.Value, cancellationToken);
        }

        return new CreateBatchResponse
        {
            Succeeded = result.Succeeded,
            BatchId = result.BatchId,
            TotalStudents = result.TotalStudents,
            FailureMessage = result.FailureMessage,
        };
    }

    private static BatchSummary MapSummary(Domain.Entities.StudentEnrollmentBatch batch) =>
        new()
        {
            BatchId = batch.Id,
            Status = batch.Status,
            TotalStudents = batch.TotalStudents,
            CompletedCount = batch.CompletedCount,
            FailedCount = batch.FailedCount,
            PendingCount = batch.PendingCount + batch.DownloadingCount + batch.ValidatingCount + batch.EmbeddingCount,
            CollegeId = batch.CollegeId,
            AcademicYear = batch.AcademicYear,
            CreatedUtc = batch.CreatedUtc,
            CompletedUtc = batch.CompletedUtc,
            ProgressPercent = CalculateBatchProgressPercent(batch),
            PhotoProviderName = batch.PhotoProviderName,
        };

    private static decimal CalculateBatchProgressPercent(Domain.Entities.StudentEnrollmentBatch batch) =>
        EnrollmentProgressCalculator.MapStatistics(MapBatchCounters(batch)).CompletionPercentage;

    private static StudentEnrollmentBatchCounters MapBatchCounters(Domain.Entities.StudentEnrollmentBatch batch) =>
        new()
        {
            TotalStudents = batch.TotalStudents,
            PendingCount = batch.PendingCount,
            DownloadingCount = batch.DownloadingCount,
            ValidatingCount = batch.ValidatingCount,
            EmbeddingCount = batch.EmbeddingCount,
            CompletedCount = batch.CompletedCount,
            FailedCount = batch.FailedCount,
            RetryRequiredCount = batch.RetryRequiredCount,
            CancelledCount = batch.CancelledCount,
        };

    private static BatchProgressState MapProgressState(BatchStatus status) => status switch
    {
        BatchStatus.Created => BatchProgressState.Queued,
        BatchStatus.Running => BatchProgressState.Downloading,
        BatchStatus.Completed => BatchProgressState.Completed,
        BatchStatus.PartiallyFailed => BatchProgressState.Failed,
        BatchStatus.Cancelled => BatchProgressState.Cancelled,
        _ => BatchProgressState.Queued,
    };

    private static StudentEnrollmentExplorerItem MapStudent(Domain.Entities.StudentEnrollmentItem item, string studentNumber) =>
        new()
        {
            ItemId = item.Id,
            StudentId = item.StudentId,
            StudentNumber = studentNumber,
            Status = item.Status,
            PhotoStatus = item.DownloadedUtc.HasValue ? "Downloaded" : item.DownloadStartedUtc.HasValue ? "Downloading" : "Pending",
            ValidationStatus = item.ValidatedUtc.HasValue ? "Validated" : "Pending",
            EmbeddingStatus = item.CompletedUtc.HasValue ? "Generated" : item.Status == EnrollmentStatus.Embedding ? "Processing" : "Pending",
            UploadStatus = string.IsNullOrWhiteSpace(item.PhotoKey) ? "Pending" : "Uploaded",
            RecognitionReady = item.Status == EnrollmentStatus.Completed,
            FailureReason = item.LastError,
            RetryCount = item.RetryCount,
            DownloadUrl = item.SourceUrl,
            ArtifactStatus = item.Status == EnrollmentStatus.Completed ? "Ready" : "Pending",
        };
}

public sealed class BatchCancellationService : IBatchCancellationService
{
    private readonly IEnrollmentBatchService _batchService;
    private readonly IEnrollmentSignalRPublisher _eventPublisher;
    private readonly IAuditService _auditService;

    public BatchCancellationService(
        IEnrollmentBatchService batchService,
        IEnrollmentSignalRPublisher eventPublisher,
        IAuditService auditService)
    {
        _batchService = batchService;
        _eventPublisher = eventPublisher;
        _auditService = auditService;
    }

    public async Task<BatchCommandResponse> CancelAsync(Guid batchId, int tenantId, int userId, CancellationToken cancellationToken = default)
    {
        var result = await _batchService.CancelBatchAsync(batchId, tenantId, userId, cancellationToken);

        if (result.Applied)
        {
            await _eventPublisher.PublishCancelledAsync(tenantId, batchId, cancellationToken);
            await _auditService.RecordAsync("StudentEnrollmentBatch", batchId.ToString(), Domain.Enums.AuditAction.Cancelled, newValues: new { Action = "Cancel", UserId = userId });
        }

        return new BatchCommandResponse
        {
            Applied = result.Applied,
            Status = result.Status,
            Message = result.Reason,
        };
    }
}

public sealed class BatchRetryService : IBatchRetryService
{
    private readonly IEnrollmentBatchService _batchService;
    private readonly IAuditService _auditService;

    public BatchRetryService(IEnrollmentBatchService batchService, IAuditService auditService)
    {
        _batchService = batchService;
        _auditService = auditService;
    }

    public async Task<BatchCommandResponse> RetryAsync(Guid batchId, int tenantId, int userId, CancellationToken cancellationToken = default)
    {
        var result = await _batchService.ResumeBatchAsync(batchId, tenantId, userId, cancellationToken);

        if (result.Applied)
        {
            await _auditService.RecordAsync("StudentEnrollmentBatch", batchId.ToString(), Domain.Enums.AuditAction.Updated, newValues: new { Action = "Retry", UserId = userId });
        }

        return new BatchCommandResponse
        {
            Applied = result.Applied,
            Status = result.Status,
            Message = result.Reason,
        };
    }
}
