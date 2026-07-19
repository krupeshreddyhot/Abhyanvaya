namespace Abhyanvaya.Application.PhotoAcquisition;

public sealed record PhotoAcquisitionStudentMaster
{
    public required int TenantId { get; init; }
    public required int StudentId { get; init; }
    public required string StudentNumber { get; init; }
    public required string CollegeCode { get; init; }
    public required int AcademicYear { get; init; }
    public string? PreferredProviderName { get; init; }
}

public sealed record PhotoSourceResolution
{
    public required string ProviderName { get; init; }
    public required PhotoAcquisitionStudentMaster Student { get; init; }
    public required string SourceReference { get; init; }
}

public sealed record PhotoDownloadResult
{
    public required bool Success { get; init; }
    public byte[]? PhotoBytes { get; init; }
    public string? ContentType { get; init; }
    public required string SourceReference { get; init; }
    public string? FailureReason { get; init; }
    public bool IsRetryable { get; init; }
}

public sealed record PhotoValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? DetectedFormat { get; init; }
    public bool IsDuplicate { get; init; }
}

public sealed record PhotoQualityReport
{
    public required decimal BlurScore { get; init; }
    public required decimal Brightness { get; init; }
    public required decimal Contrast { get; init; }
    public decimal FaceVisibilityScore { get; init; }
    public decimal RotationDegrees { get; init; }
    public decimal OcclusionScore { get; init; }
    public required decimal OverallScore { get; init; }
}

public sealed record PhotoDownloadManifestEntry
{
    public required Guid ItemId { get; init; }
    public required int StudentId { get; init; }
    public required string StudentNumber { get; init; }
    public required string SourceReference { get; init; }
    public string? ContentHash { get; init; }
    public string? ContentType { get; init; }
    public int? PhotoByteSize { get; init; }
    public decimal? QualityScore { get; init; }
}

public sealed record PhotoDownloadManifest
{
    public required Guid BatchId { get; init; }
    public required int TenantId { get; init; }
    public required string ProviderName { get; init; }
    public required IReadOnlyList<PhotoDownloadManifestEntry> Entries { get; init; }
    public required IReadOnlyList<PhotoDownloadManifestEntry> FailedEntries { get; init; }
    public required IReadOnlyList<PhotoDownloadManifestEntry> RetryEntries { get; init; }
    public DateTime GeneratedUtc { get; init; }
}

public sealed record PhotoAcquisitionBatchRequest
{
    public required int TenantId { get; init; }
    public required string ProviderName { get; init; }
    public required int AcademicYear { get; init; }
    public required IReadOnlyList<PhotoAcquisitionStudentMaster> Students { get; init; }
}

public sealed record PhotoAcquisitionBatchResult
{
    public required Guid BatchId { get; init; }
    public required PhotoDownloadManifest Manifest { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public int RetryQueuedCount { get; init; }
    public int ReadyForEnrollmentCount { get; init; }
}

public sealed record PhotoAcquisitionReport
{
    public required Guid BatchId { get; init; }
    public required PhotoDownloadManifest Manifest { get; init; }
    public required IReadOnlyList<PhotoQualityReport> QualityReports { get; init; }
    public required IReadOnlyList<string> FailedDownloads { get; init; }
    public int EnrollmentQueueDepth { get; init; }
}

public sealed class PhotoAcquisitionOptions
{
    public const string SectionName = "PhotoAcquisition";

    public int MaxConcurrentDownloads { get; set; } = 16;

    public int MaxRetryAttempts { get; set; } = 3;

    public int MinimumWidth { get; set; } = 200;

    public int MinimumHeight { get; set; } = 200;

    public long MaximumByteSize { get; set; } = 5 * 1024 * 1024;

    public int DownloadTimeoutSeconds { get; set; } = 30;
}
