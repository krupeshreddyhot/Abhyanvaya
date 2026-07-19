using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Enrollment;

public sealed record EnrollmentBatchRequest
{
    public required int TenantId { get; init; }
    public required int UniversityId { get; init; }
    public required int CollegeId { get; init; }
    public required int AcademicYear { get; init; }
    public required int RequestedByUserId { get; init; }

    public int? CourseId { get; init; }
    public int? GroupId { get; init; }

    /// <summary>Admission batch filter (<see cref="Domain.Entities.Student.Batch"/>).</summary>
    public int? Batch { get; init; }

    public int? SubjectId { get; init; }
    public string? StudentFilter { get; init; }
    public bool ForceReEnrollment { get; init; }
    public string? PhotoProvider { get; init; }
    public int Priority { get; init; }
    public Guid? CorrelationId { get; init; }
}

public enum EnrollmentBatchFailureCode
{
    InvalidRequest = 0,
    CollegeNotFound = 1,
    CourseNotFound = 2,
    GroupNotFound = 3,
    BatchNotFound = 4,
    SubjectNotFound = 5,
    ActiveBatchAlreadyRunning = 6,
    NoEligibleStudents = 7,
    PipelineVersionNotFound = 8,
    PipelineManifestNotFound = 9,
    ConfigurationSnapshotFailed = 10,
    QueueFailed = 11,
    PersistenceFailed = 12,
}

public sealed record EnrollmentBatchCreateResult
{
    public required bool Succeeded { get; init; }
    public Guid? BatchId { get; init; }
    public int TotalStudents { get; init; }
    public BatchStatus? Status { get; init; }
    public EnrollmentBatchFailureCode? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
    public Guid? CorrelationId { get; init; }
    public int? PipelineVersion { get; init; }

    public static EnrollmentBatchCreateResult Success(
        Guid batchId,
        int totalStudents,
        BatchStatus status,
        Guid correlationId,
        int pipelineVersion) =>
        new()
        {
            Succeeded = true,
            BatchId = batchId,
            TotalStudents = totalStudents,
            Status = status,
            CorrelationId = correlationId,
            PipelineVersion = pipelineVersion,
        };

    public static EnrollmentBatchCreateResult Failure(
        EnrollmentBatchFailureCode code,
        string message,
        Guid? correlationId = null) =>
        new()
        {
            Succeeded = false,
            FailureCode = code,
            FailureMessage = message,
            CorrelationId = correlationId,
        };
}

public sealed record EnrollmentCommandResult
{
    public required bool Applied { get; init; }
    public required BatchStatus Status { get; init; }
    public string? Reason { get; init; }

    public static EnrollmentCommandResult Ok(BatchStatus status) =>
        new() { Applied = true, Status = status };

    public static EnrollmentCommandResult NoOp(BatchStatus status, string reason) =>
        new() { Applied = false, Status = status, Reason = reason };
}

public sealed record EnrollmentEligibleStudent
{
    public required int StudentId { get; init; }
    public required string StudentNumber { get; init; }
}

public sealed record EnrollmentStudentDiscoveryCriteria
{
    public required int TenantId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? Batch { get; init; }
    public int? SubjectId { get; init; }
    public string? StudentFilter { get; init; }
    public bool ForceReEnrollment { get; init; }
}
