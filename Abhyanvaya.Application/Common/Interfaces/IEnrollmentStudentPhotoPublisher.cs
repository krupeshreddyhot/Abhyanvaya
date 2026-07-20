namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentStudentPhotoPublisher
{
    Task<EnrollmentStudentPhotoPublishResult> PublishAsync(
        EnrollmentStudentPhotoPublishRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EnrollmentStudentPhotoPublishRequest
{
    public required int TenantId { get; init; }
    public required int StudentId { get; init; }
    public required Guid BatchId { get; init; }
    public required Guid ItemId { get; init; }
    public required byte[] PhotoBytes { get; init; }
    public string? ContentType { get; init; }
}

public sealed record EnrollmentStudentPhotoPublishResult
{
    public required bool Success { get; init; }
    public string? PhotoKey { get; init; }
    public DateTime? PhotoUploadedUtc { get; init; }
    public string? FailureReason { get; init; }

    public static EnrollmentStudentPhotoPublishResult Succeeded(string photoKey, DateTime uploadedUtc) =>
        new() { Success = true, PhotoKey = photoKey, PhotoUploadedUtc = uploadedUtc };

    public static EnrollmentStudentPhotoPublishResult Failed(string reason) =>
        new() { Success = false, FailureReason = reason };
}
