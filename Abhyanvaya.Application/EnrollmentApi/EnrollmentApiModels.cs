using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.EnrollmentApi;

public sealed record EnrollmentDashboardDto
{
    public required int TotalStudents { get; init; }
    public required int EligibleStudents { get; init; }
    public required int Embedded { get; init; }
    /// <summary>Completed enrollment items with a profile photo but no face embedding.</summary>
    public required int UploadedWithoutEmbedding { get; init; }
    public required int Pending { get; init; }
    public required int Failed { get; init; }
    public required int ProcessedToday { get; init; }
    public Guid? RunningBatchId { get; init; }
    public required int QueueLength { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public decimal SuccessRate { get; init; }
}

public sealed record EnrollmentReadinessResult
{
    public required bool CanStart { get; init; }
    public required int EligibleStudents { get; init; }
    public Guid? RunningBatchId { get; init; }
    public required bool PhotoProviderReady { get; init; }
    public required bool StorageReady { get; init; }
    public required bool RecognitionReady { get; init; }
    public required bool WorkerReady { get; init; }
    public required bool ConfigurationValid { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
}

public sealed record EnrollmentFilters
{
    public int? CollegeId { get; init; }
    public int? UniversityId { get; init; }
    public int? AcademicYear { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? Batch { get; init; }
    public int? SubjectId { get; init; }
    public string? Search { get; init; }
    public BatchStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;
}

public sealed record EnrollmentPreviewRequest
{
    public required int TenantId { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? Batch { get; init; }
    public int? SubjectId { get; init; }
    public bool ForceReEnrollment { get; init; }
}

public sealed record EnrollmentPreview
{
    public required int EligibleStudentCount { get; init; }
    public required IReadOnlyList<string> SampleStudentNumbers { get; init; }
}

public sealed record CreateEnrollmentBatchApiRequest
{
    public required int UniversityId { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? Batch { get; init; }
    public int? SubjectId { get; init; }
    public bool ForceReEnrollment { get; init; }
    public string? PhotoProvider { get; init; }
}

public record BatchSummary
{
    public required Guid BatchId { get; init; }
    public required BatchStatus Status { get; init; }
    public required int TotalStudents { get; init; }
    public required int CompletedCount { get; init; }
    public required int UploadedWithoutEmbedding { get; init; }
    public required int FailedCount { get; init; }
    public required int PendingCount { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public decimal ProgressPercent { get; init; }
    public string? PhotoProviderName { get; init; }
}

public sealed record BatchDetailDto : BatchSummary
{
    public required int UniversityId { get; init; }
    public required int CreatedBy { get; init; }
    public DateTime? StartedUtc { get; init; }
    public Guid CorrelationId { get; init; }
    public int PipelineVersion { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
}

public sealed record BatchProgressDto
{
    public required Guid BatchId { get; init; }
    public required BatchProgressState State { get; init; }
    public required decimal Percentage { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }
    public required int Queued { get; init; }
    public required int Downloading { get; init; }
    public required int Validating { get; init; }
    public required int Embedding { get; init; }
    public required int Completed { get; init; }
    public required int UploadedWithoutEmbedding { get; init; }
    public required int Failed { get; init; }
    public required int Cancelled { get; init; }
}

public enum BatchProgressState
{
    Queued = 0,
    Downloading = 1,
    Validating = 2,
    Embedding = 3,
    Uploading = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7,
}

public sealed record StudentEnrollmentExplorerItem
{
    public required Guid ItemId { get; init; }
    public required int StudentId { get; init; }
    public required string StudentNumber { get; init; }
    public required EnrollmentStatus Status { get; init; }
    public required string PhotoStatus { get; init; }
    public required string ValidationStatus { get; init; }
    public required string EmbeddingStatus { get; init; }
    public required string UploadStatus { get; init; }
    public required bool RecognitionReady { get; init; }
    public string? FailureReason { get; init; }
    public required int RetryCount { get; init; }
    public string? DownloadUrl { get; init; }
    public required string ArtifactStatus { get; init; }
}

public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

public sealed record EnrollmentConfigurationDto
{
    public required string PhotoProvider { get; init; }
    public required string EmbeddingEngine { get; init; }
    public required string RecognitionEngine { get; init; }
    public required string StorageProvider { get; init; }
    public required string RetryPolicy { get; init; }
    public required int DownloadThreads { get; init; }
    public required string ImageFormat { get; init; }
    public required int EmbeddingDimensions { get; init; }
    public required string PhotoUrlTemplate { get; init; }
}

public sealed record EnrollmentSystemStatusDto
{
    public required string PhotoProvider { get; init; }
    public required string PhotoProviderStatus { get; init; }
    public required string EmbeddingEngine { get; init; }
    public required string EmbeddingEngineStatus { get; init; }
    public required string RecognitionEngine { get; init; }
    public required string RecognitionEngineStatus { get; init; }
    public required string StorageProvider { get; init; }
    public required string StorageStatus { get; init; }
    public required string WorkerStatus { get; init; }
}

public sealed record EnrollmentDashboardResponse
{
    public required EnrollmentDashboardDto Dashboard { get; init; }
    public required EnrollmentSystemStatusDto SystemStatus { get; init; }
    public required EnrollmentConfigurationDto Configuration { get; init; }
}

public sealed record BatchCommandResponse
{
    public required bool Applied { get; init; }
    public required BatchStatus Status { get; init; }
    public string? Message { get; init; }
}

public sealed record CreateBatchResponse
{
    public required bool Succeeded { get; init; }
    public Guid? BatchId { get; init; }
    public int TotalStudents { get; init; }
    public string? FailureMessage { get; init; }
}
