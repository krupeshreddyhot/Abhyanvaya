namespace Abhyanvaya.Application.DTOs.Student;

public sealed class StudentPhotoUploadResult
{
    public required string PhotoKey { get; init; }

    public DateTime PhotoUploadedUtc { get; init; }

    public bool PhotoVerified { get; init; }

    public string? OriginalUrl { get; init; }

    public string? ThumbnailUrl { get; init; }
}
