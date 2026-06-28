namespace Abhyanvaya.Application.DTOs.Student;

/// <summary>Student photo metadata returned by GET /api/student/{id}/photo.</summary>
public sealed class StudentPhotoDto
{
    public bool HasPhoto { get; init; }

    public string? PhotoKey { get; init; }

    public DateTime? PhotoUploadedUtc { get; init; }

    public bool PhotoVerified { get; init; }

    public string? OriginalUrl { get; init; }

    public string? ThumbnailUrl { get; init; }
}
