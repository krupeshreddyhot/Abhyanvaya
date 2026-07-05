using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Creates AI photo attendance sessions and coordinates transactional upload.
/// </summary>
public interface IAttendanceSessionCreator
{
    Task<(bool Ok, string? Error, Guid? SessionId)> CreatePhotoAttendanceSessionAsync(
        CreatePhotoAttendanceSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> CreateAndUploadClassroomPhotoAsync(
        CreatePhotoAttendanceSessionRequest request,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);
}

public sealed class CreatePhotoAttendanceSessionRequest
{
    public int CourseId { get; init; }

    public int GroupId { get; init; }

    public int SemesterId { get; init; }

    public int SubjectId { get; init; }

    public DateTime AttendanceDate { get; init; }

    public int PeriodNumber { get; init; }

    public short SessionNumber { get; init; } = 1;

    public int TotalStudents { get; init; }

    public string? RecognitionPipelineVersion { get; init; }
}
