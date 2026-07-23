namespace Abhyanvaya.Domain.ValueObjects;

/// <summary>
/// Classroom photo capture and storage metadata for an attendance session.
/// </summary>
public sealed class ClassroomImageMetadata
{
    public string? ImageKey { get; set; }

    public string? ImageHash { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public short? Orientation { get; set; }

    public DateTime? CaptureTimestamp { get; set; }

    public string? CaptureDevice { get; set; }

    public DateTime? UploadedUtc { get; set; }

    public long? FileSize { get; set; }

    /// <summary>Acquisition channel: Upload, CameraCapture, or CameraMultiCapture (AI22.7A).</summary>
    public string? AcquisitionMethod { get; set; }

    /// <summary>Optional client-reported capture latitude (WGS84), when permission granted.</summary>
    public double? CaptureLatitude { get; set; }

    /// <summary>Optional client-reported capture longitude (WGS84), when permission granted.</summary>
    public double? CaptureLongitude { get; set; }

    /// <summary>Optional client-side blur score (higher = sharper). Soft quality signal only.</summary>
    public double? BlurScore { get; set; }

    public bool HasUploadedImage => !string.IsNullOrWhiteSpace(ImageKey);
}
