using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// One classroom photo belonging to an <see cref="AttendanceSession"/> (AI22.7A Phase 2).
/// Sessions may hold 1–10 images; recognition merges faces from all images into one review set.
/// </summary>
public sealed class AttendanceSessionImage : ITenantScoped
{
    public Guid Id { get; set; }

    public int TenantId { get; set; }

    public Guid AttendanceSessionId { get; set; }

    /// <summary>1-based display/processing order; matches <see cref="AttendanceRecognition.ImageSequence"/>.</summary>
    public short ImageSequence { get; set; }

    public string ImageKey { get; set; } = null!;

    public string? ImageHash { get; set; }

    public string? OriginalFileName { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public short? Orientation { get; set; }

    public long? FileSize { get; set; }

    public DateTime? UploadedUtc { get; set; }

    public DateTime? CaptureTimestamp { get; set; }

    public string? CaptureDevice { get; set; }

    public string? AcquisitionMethod { get; set; }

    public double? CaptureLatitude { get; set; }

    public double? CaptureLongitude { get; set; }

    public double? BlurScore { get; set; }

    public string? ThumbnailImageKey { get; set; }

    public string? AnnotatedImageKey { get; set; }

    public AttendanceSessionImageStatus Status { get; set; } = AttendanceSessionImageStatus.Uploaded;

    public string? ProcessingError { get; set; }

    public DateTime CreatedUtc { get; set; }

    public AttendanceSession? AttendanceSession { get; set; }
}
