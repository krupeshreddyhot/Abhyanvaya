namespace Abhyanvaya.Application.DTOs.Attendance;

/// <summary>
/// Optional client capture context for classroom photo upload (AI22.7A Phase 1).
/// Backward compatible — all fields optional; legacy upload clients omit this entirely.
/// </summary>
public sealed class ClassroomPhotoCaptureContextDto
{
    /// <summary>Upload | CameraCapture | CameraMultiCapture</summary>
    public string? AcquisitionMethod { get; set; }

    public string? CaptureDevice { get; set; }

    public DateTime? CaptureTimestampUtc { get; set; }

    public short? Orientation { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Client Laplacian-variance style blur score (higher = sharper).</summary>
    public double? BlurScore { get; set; }
}
