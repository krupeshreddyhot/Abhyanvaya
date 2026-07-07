namespace Abhyanvaya.Application.DTOs.Recognition;

/// <summary>Face detection output; no attendance side effects.</summary>
public sealed class FaceDetectionResponse
{
    public string Provider { get; set; } = null!;

    public string Model { get; set; } = null!;

    public string Version { get; set; } = null!;

    public int ImageWidth { get; set; }

    public int ImageHeight { get; set; }

    public int DetectionDurationMs { get; set; }

    public IReadOnlyList<DetectedFaceDto> Faces { get; set; } = [];
}
