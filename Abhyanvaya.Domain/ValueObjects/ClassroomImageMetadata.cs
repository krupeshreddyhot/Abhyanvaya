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

    public bool HasUploadedImage => !string.IsNullOrWhiteSpace(ImageKey);
}
