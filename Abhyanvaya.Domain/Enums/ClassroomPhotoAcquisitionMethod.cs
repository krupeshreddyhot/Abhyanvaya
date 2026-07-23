namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// How a classroom photo was acquired for an AI attendance session (AI22.7A Phase 1).
/// </summary>
public enum ClassroomPhotoAcquisitionMethod : short
{
    /// <summary>Traditional file picker / drag-and-drop upload.</summary>
    Upload = 1,

    /// <summary>Single frame captured from a device camera.</summary>
    CameraCapture = 2,

    /// <summary>One frame selected from a multi-capture camera gallery.</summary>
    CameraMultiCapture = 3,
}
