using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;

namespace Abhyanvaya.Domain.Entities;

public partial class AttendanceSession
{
    /// <summary>Classroom image metadata (storage key, dimensions, capture context).</summary>
    public ClassroomImageMetadata ImageMetadata { get; private set; } = new();

    /// <summary>Storage key for a thumbnail preview of the session image.</summary>
    public string? ThumbnailImageKey { get; private set; }

    /// <summary>Storage key for the annotated image with detected face overlays.</summary>
    public string? AnnotatedImageKey { get; private set; }

    /// <summary>Original filename as uploaded by the client.</summary>
    public string? OriginalFileName { get; private set; }

    /// <summary>Attaches a validated classroom photo to this session.</summary>
    public void AttachClassroomImage(
        string storageKey,
        string fileName,
        ClassroomImageMetadata metadata,
        DateTime uploadedUtc,
        long fileSizeBytes)
    {
        ImageMetadata = metadata;
        ImageMetadata.ImageKey = storageKey;
        ImageMetadata.UploadedUtc = uploadedUtc;
        ImageMetadata.FileSize = fileSizeBytes;
        OriginalFileName = fileName;
        AttendanceMethod = AttendanceMethod.AIPhoto;
    }

    /// <summary>Updates image dimensions after AI detection.</summary>
    public void SetImageDimensions(int width, int height)
    {
        ImageMetadata.Width = width;
        ImageMetadata.Height = height;
    }

    /// <summary>Clears denormalized primary classroom image metadata (collection empty).</summary>
    public void ClearClassroomImage()
    {
        ImageMetadata = new ClassroomImageMetadata();
        OriginalFileName = null;
        ThumbnailImageKey = null;
        AnnotatedImageKey = null;
    }
}
