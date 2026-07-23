using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.Attendance;

public sealed class AttendanceSessionImageDto
{
    public Guid Id { get; set; }

    public short ImageSequence { get; set; }

    public string? ImageUrl { get; set; }

    public string? OriginalFileName { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long? FileSize { get; set; }

    public DateTime? UploadedUtc { get; set; }

    /// <summary>AI22.7A Phase 3 — device capture time when available.</summary>
    public DateTime? CaptureTimestamp { get; set; }

    public string? CaptureDevice { get; set; }

    public double? CaptureLatitude { get; set; }

    public double? CaptureLongitude { get; set; }

    public short? Orientation { get; set; }

    public string? AcquisitionMethod { get; set; }

    public double? BlurScore { get; set; }

    public AttendanceSessionImageStatus Status { get; set; }

    public string? ProcessingError { get; set; }

    public string ImageStorageKey { get; set; } = null!;

    /// <summary>Faces detected for this image sequence (from AttendanceRecognition).</summary>
    public int DetectedFaceCount { get; set; }

    /// <summary>Enterprise batch status label derived from Status (Waiting / Processing / Processed / Failed).</summary>
    public string BatchStatus { get; set; } = "Waiting";
}

public sealed class ReorderSessionImagesRequestDto
{
    /// <summary>Ordered list of session image IDs (new display order).</summary>
    public List<Guid> ImageIds { get; set; } = [];
}

public sealed class ClassroomPhotoCollectionUploadResult
{
    public Guid AttendanceSessionId { get; set; }

    public AttendanceSessionImageDto Image { get; set; } = null!;

    public bool Queued { get; set; }

    public int ImageCount { get; set; }

    /// <summary>AI22.7A Phase 3 — recognition scope used when queuing after replace/add.</summary>
    public string? RecognitionScope { get; set; }
}

/// <summary>AI22.7A Phase 2 — max classroom images per attendance session.</summary>
public static class ClassroomPhotoCollectionLimits
{
    public const int MaxImagesPerSession = 10;
}
