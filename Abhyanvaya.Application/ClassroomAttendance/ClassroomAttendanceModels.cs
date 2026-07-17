using Abhyanvaya.Application.DTOs.Recognition;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.ClassroomAttendance;

public enum AttendanceDecisionType
{
    Present = 0,
    Absent = 1,
    Late = 2,
    Unknown = 3,
    ManualReview = 4,
    Duplicate = 5,
    Rejected = 6,
}

public enum AttendanceConflictType
{
    DuplicateFace = 0,
    DuplicateStudent = 1,
    MultipleCandidates = 2,
    UnknownFace = 3,
    BorderlineConfidence = 4,
}

public sealed record AttendanceSessionMetadata
{
    public required Guid SessionId { get; init; }
    public required int TenantId { get; init; }
    public required int CourseId { get; init; }
    public required int GroupId { get; init; }
    public required int SemesterId { get; init; }
    public required int SubjectId { get; init; }
    public required DateTime AttendanceDateUtc { get; init; }
    public string? ImageStorageKey { get; init; }
}

public sealed record FaceRecognitionOutcome
{
    public required int FaceIndex { get; init; }
    public required DetectedFaceDto DetectedFace { get; init; }
    public RecognitionResult? RecognitionResult { get; init; }
    public string? FaceImageKey { get; init; }
}

public sealed record AttendanceDecision
{
    public required int FaceIndex { get; init; }
    public required AttendanceDecisionType DecisionType { get; init; }
    public int? StudentId { get; init; }
    public Guid? RecognitionId { get; init; }
    public decimal? Confidence { get; init; }
    public RecognitionStatus RecognitionStatus { get; init; }
    public string? Reason { get; init; }
    public bool RequiresManualReview { get; init; }
}

public sealed record AttendanceConflict
{
    public required AttendanceConflictType ConflictType { get; init; }
    public required int FaceIndex { get; init; }
    public int? StudentId { get; init; }
    public string? Description { get; init; }
}

public sealed record AttendanceSessionStatistics
{
    public int StudentsPresent { get; init; }
    public int StudentsAbsent { get; init; }
    public int UnknownFaces { get; init; }
    public int Duplicates { get; init; }
    public int ManualReviews { get; init; }
    public int DetectedFaces { get; init; }
    public TimeSpan RecognitionTime { get; init; }
    public TimeSpan DecisionTime { get; init; }
    public TimeSpan PersistenceTime { get; init; }
    public TimeSpan TotalDuration { get; init; }
}

public sealed record AttendanceSessionContext
{
    public required AttendanceSessionMetadata Session { get; init; }
    public required Guid CorrelationId { get; init; }
    public AttendanceSessionState State { get; init; } = AttendanceSessionState.Created;
    public IReadOnlyList<DetectedFaceDto>? DetectedFaces { get; init; }
    public IReadOnlyList<FaceRecognitionOutcome>? RecognitionOutcomes { get; init; }
    public IReadOnlyList<AttendanceConflict>? Conflicts { get; init; }
    public IReadOnlyList<AttendanceDecision>? Decisions { get; init; }
    public IAttendancePolicy? Policy { get; init; }
    public AttendanceSessionStatistics? Statistics { get; init; }
    public bool CancellationRequested { get; init; }
}

public sealed record AttendanceSessionResult
{
    public required bool Success { get; init; }
    public required Guid SessionId { get; init; }
    public required int TenantId { get; init; }
    public AttendanceSessionState State { get; init; }
    public IReadOnlyList<AttendanceDecision>? Decisions { get; init; }
    public AttendanceSessionStatistics? Statistics { get; init; }
    public string? FailureReason { get; init; }
    public Guid CorrelationId { get; init; }
}

public sealed record AttendanceValidationResult
{
    public required bool IsValid { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    public IReadOnlyList<FaceRecognitionOutcome>? ValidOutcomes { get; init; }
}

public sealed record AttendanceConflictResolutionResult
{
    public required IReadOnlyList<FaceRecognitionOutcome> ResolvedOutcomes { get; init; }
    public required IReadOnlyList<AttendanceConflict> ResolvedConflicts { get; init; }
}

public sealed record AttendancePersistenceRequest
{
    public required AttendanceSessionContext Context { get; init; }
    public required IReadOnlyList<AttendanceDecision> Decisions { get; init; }
    public required AttendanceSessionStatistics Statistics { get; init; }
}

public sealed record AttendancePersistenceResult
{
    public required bool Success { get; init; }
    public int DecisionsPersisted { get; init; }
    public string? FailureReason { get; init; }
}

public interface IAttendancePolicy
{
    float MinimumConfidence { get; }
    bool RequireTeacherApproval { get; }
    bool ManualReviewEnabled { get; }
    TimeSpan? AttendanceWindowStart { get; }
    TimeSpan? AttendanceWindowEnd { get; }
    TimeSpan LateArrivalThreshold { get; }
    bool AllowDuplicateStudents { get; }
    bool AllowReRecognition { get; }
    float UnknownFaceThreshold { get; }
}

public sealed record ManualReviewRequest
{
    public required Guid SessionId { get; init; }
    public required int FaceIndex { get; init; }
    public required string Reason { get; init; }
    public Guid? RecognitionId { get; init; }
}

public sealed record ManualReviewResult
{
    public required bool RequiresReview { get; init; }
    public string? ReviewReason { get; init; }
}

public sealed record AttendanceAnalyticsSnapshot
{
    public required Guid SessionId { get; init; }
    public decimal RecognitionAccuracyPercent { get; init; }
    public decimal AttendanceAccuracyPercent { get; init; }
    public int TeacherCorrections { get; init; }
    public int FalsePositives { get; init; }
    public int FalseNegatives { get; init; }
    public decimal UnknownRatePercent { get; init; }
}
