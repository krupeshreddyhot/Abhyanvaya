using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

public class StudentPhotoAcquisitionBatch
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public required string ProviderName { get; set; }

    public int AcademicYear { get; set; }

    public PhotoAcquisitionBatchStatus Status { get; set; } = PhotoAcquisitionBatchStatus.Created;

    public int TotalItems { get; set; }

    public int SucceededCount { get; set; }

    public int FailedCount { get; set; }

    public int RetryQueuedCount { get; set; }

    public int ReadyForEnrollmentCount { get; set; }

    public string? ManifestJson { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public ICollection<StudentPhotoAcquisitionItem> Items { get; set; } = new List<StudentPhotoAcquisitionItem>();
}

public class StudentPhotoAcquisitionItem
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public int TenantId { get; set; }

    public int StudentId { get; set; }

    public required string StudentNumber { get; set; }

    public required string CollegeCode { get; set; }

    public PhotoAcquisitionItemStatus Status { get; set; } = PhotoAcquisitionItemStatus.Pending;

    public string? SourceReference { get; set; }

    public string? ContentType { get; set; }

    public string? ContentHash { get; set; }

    public int? PhotoByteSize { get; set; }

    public byte[]? PhotoBytes { get; set; }

    public string? ValidationReportJson { get; set; }

    public string? QualityReportJson { get; set; }

    public string? FailureReason { get; set; }

    public int RetryCount { get; set; }

    public DateTime? NextAttemptUtc { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    public StudentPhotoAcquisitionBatch Batch { get; set; } = null!;
}
